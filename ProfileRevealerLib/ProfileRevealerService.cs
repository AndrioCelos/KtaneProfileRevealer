#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;

using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;

#if !DEBUG
using UnityEngine.SceneManagement;

using Assets.Scripts.Missions;
using Assets.Scripts.Services;

using InControl;
#endif

namespace ProfileRevealerLib {
	public class ProfileRevealerService : MonoBehaviour {
#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
		public ModulePopup PopupPrefab;
		public GameObject AdvantageousWarningCanvas;

		private KMModSettings KMModSettings;
		private KMGameInfo KMGameInfo;
		private KMGamepad KMGamepad;
		private Config config;
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
		private KMGameInfo.State gameState;
		private ModulePopup? highlightedModulePopup;
		private ModulePopup? focusedModulePopup;
		private Component? tweaksService;
		private IDictionary<string, IList<string>>? moduleProfiles;
		private IDictionary<string, IList<string>> bossStatus = new Dictionary<string, IList<string>>();

		private readonly List<ModulePopup> popups = new List<ModulePopup>();

#if !DEBUG
		private List<KTTrackedController>? vrControllers;
		private static readonly FieldInfo currentSelectableField = typeof(KTTrackedController).GetField("currentSelectable", BindingFlags.NonPublic | BindingFlags.Instance);
#endif

		private FieldInfo? tweaksSettingsField;
		private FieldInfo? tweaksDisableAdvantageousField;

		public void Start() {
			this.AdvantageousWarningCanvas.SetActive(false);

			this.KMGameInfo = this.GetComponent<KMGameInfo>();
			this.KMModSettings = this.GetComponent<KMModSettings>();
			this.KMGamepad = this.GetComponent<KMGamepad>();

			if (Application.isEditor) {
				this.config = new Config { ShowModuleNames = true };
				this.StartCoroutine(this.CheckForBombsTest());
			} else {
				this.RefreshConfig();
			}

			GetModuleJSON();

#if !DEBUG
			this.KMGameInfo.OnStateChange = this.KMGameInfo_OnStateChange;
			UnityEngine.SceneManagement.SceneManager.sceneLoaded += this.SceneManager_sceneLoaded;
			if (KTInputManager.Instance.IsMotionControlMode())
				this.StartCoroutine(this.SearchForVrControllersCoroutine());
#endif
		}

#if !DEBUG
		private IEnumerator SearchForVrControllersCoroutine() {
			Debug.Log($"[Provile Revealer] Motion controls are active. Searching for VR controllers.");
			this.vrControllers = new List<KTTrackedController>();
			while (true) {
				foreach (var controller in KTInputManager.Instance.MotionControls.Controllers) {
					if (!this.vrControllers.Contains(controller)) {
						this.vrControllers.Add(controller);
						var steamVRController = controller.GetComponent<SteamVR_TrackedController>();
						if (steamVRController != null) {
							Debug.Log($"[Provile Revealer] Found a Steam VR controller.");
							steamVRController.Gripped += (sender, e) => this.VrButtonPressed(controller);
						} else {
							var oculusVRController = controller.GetComponent<KTOculusTouchDevice>();
							if (oculusVRController != null) {
								Debug.Log($"[Provile Revealer] Found an Oculus VR controller.");
								oculusVRController.ButtonTwoPressed += () => this.VrButtonPressed(controller);
								oculusVRController.ButtonFourPressed += () => this.VrButtonPressed(controller);
							} else
								Debug.LogWarning($"[Provile Revealer] Found an unknown VR controller.");
						}
					}
				}
				yield return new WaitForSeconds(10);
			}
		}

		private void VrButtonPressed(KTTrackedController controller) {
			if (this.highlightedModulePopup != null) {
				this.highlightedModulePopup.Hide();
				this.highlightedModulePopup = null;
			}
			var selectable = (Selectable) currentSelectableField.GetValue(controller);
			Debug.Log($"[Provile Revealer] Grip button pressed on {selectable}.");
			while (selectable != null && selectable.GetComponent<BombComponent>() == null)
				selectable = selectable.Parent;
			Debug.Log($"[Provile Revealer] Module parent is {selectable}.");
			if (selectable != null) {
				var popup = this.popups.FirstOrDefault(p => p.Module == selectable.transform);
				if (popup != null) {
					this.highlightedModulePopup = popup;
					this.SetProfileName(popup);
					popup.Show();
				}
			}
		}
#endif

		private void GetModuleJSON()
		{
			Debug.Log($"[Provile Revealer] Starting request for boss status..");
			bossStatus = new Dictionary<string, IList<string>>();
			StartCoroutine(GetModuleJSONCoroutine());
		}

		private IEnumerator GetModuleJSONCoroutine()
		{
			var url = "https://buriburi/json/raw";
            using var http = UnityWebRequest.Get(url);

            yield return http.SendWebRequest();

            if (http.isNetworkError)
            {
                Debug.LogFormat(@"[Profile Revealer] Website {0} responded with error: {1}", url, http.error);
                yield break;
            }

            if (http.responseCode != 200)
            {
                Debug.LogFormat(@"[Profile Revealer] Website {0} responded with code: {1}", url, http.responseCode);
                yield break;
            }

            var allModules = JObject.Parse(http.downloadHandler.text)["KtaneModules"] as JArray;
            if (allModules == null)
            {
                Debug.LogFormat(@"[Profile Revealer] Website {0} did not respond with a JSON array at “KtaneModules” key.", url, http.responseCode);
                yield break;
            }

            foreach (JObject module in allModules)
            {
                var status = new List<string>() { };
                if (module["IsFullBoss"] is JValue isFullBossV && isFullBossV.Value is bool isFullBoss && isFullBoss == true)
                {
                    status.Add("Full Boss");
                }
                if (module["IsSemiBoss"] is JValue isSemiBossV && isSemiBossV.Value is bool isSemiBoss && isSemiBoss == true)
                {
                    status.Add("Semi Boss");
                }
                if (module["IsPseudoNeedy"] is JValue isPseudoNeedyV && isPseudoNeedyV.Value is bool isPsudoNeedy && isPsudoNeedy == true)
                {
                    status.Add("Pseudo Needy");
                }

				if(status.Count > 0)
				{
					if (module["DisplayName"] is JValue displayNameV && displayNameV.Value is string displayName)
					{
						bossStatus.Add(displayName, status);
						Debug.LogFormat(@"[Profile Revealer] Setting boss status of {0} to {1}.", displayName, String.Join(", ", status.ToArray()));
					} else if (module["Name"] is JValue nameV && nameV.Value is string name)
					{
						bossStatus.Add(name, status);
						Debug.LogFormat(@"[Profile Revealer] Setting boss status of {0} to {1}.", name, String.Join(", ", status.ToArray()));

					}

				}
			}
		}

		private bool prevPressed;
		public void Update() {
			if (this.gameState == KMGameInfo.State.Gameplay && this.config != null
#if !DEBUG
				&& this.vrControllers == null
#endif
				) {
				bool pressed;
				if (this.config.PopupKey != 0) {
					pressed = Input.GetKeyDown(this.config.PopupKey) && (this.config.PopupKeyModifiers == 0 || (
						((this.config.PopupKeyModifiers & ModifierKeys.Shift) == 0 || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) &&
						((this.config.PopupKeyModifiers & ModifierKeys.Ctrl) == 0 || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) &&
						((this.config.PopupKeyModifiers & ModifierKeys.Alt) == 0 || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) &&
						((this.config.PopupKeyModifiers & ModifierKeys.Command) == 0 || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand)) &&
						((this.config.PopupKeyModifiers & ModifierKeys.Super) == 0 || Input.GetKey(KeyCode.LeftWindows) || Input.GetKey(KeyCode.RightWindows))));
				} else if (this.config.PopupButton >= 0) {
					pressed = this.KMGamepad.GetButtonDown(this.config.PopupButton);
				} else {
					pressed = this.KMGamepad.GetAxisValue(this.config.PopupAxis) > 0.75f;
					if (this.prevPressed) {
						if (!pressed) this.prevPressed = false;
						pressed = false;
					} else
						this.prevPressed = pressed;
				}
				if (pressed) {
					ModulePopup popup;
					if (this.highlightedModulePopup != null) popup = this.highlightedModulePopup;
					else if (this.focusedModulePopup != null) popup = this.focusedModulePopup;
					else return;
					if (popup.Visible) popup.Hide();
					else {
						this.SetProfileName(popup);
						popup.Show();
					}
				}
			}
		}

#if !DEBUG
		private void KMGameInfo_OnStateChange(KMGameInfo.State state) {
			if (state == KMGameInfo.State.Gameplay) {
				// Enabling Show Module Names is considered an advantageous feature, so disable records in that case.
				// This code is based on the Tweaks mod.
				if (this.config.IsAdvantagusFeatures) LeaderboardController.DisableLeaderboards();
				this.StartCoroutine(this.CheckForBombs());
			} else if (state == KMGameInfo.State.Transitioning && this.gameState == KMGameInfo.State.Setup) {
				this.KMModSettings.RefreshSettings();
				this.RefreshConfig();
			} else if (state == KMGameInfo.State.Setup) {
				this.popups.Clear();
				if (this.tweaksService == null) {
					Debug.Log("[Profile Revealer] Looking for Tweaks service...");
					var obj = GameObject.Find("Tweaks(Clone)");
					if (obj != null) this.tweaksService = obj.GetComponent("Tweaks");
					if (this.tweaksService != null) Debug.Log("[Profile Revealer] Found Tweaks service.");
					else {
						Debug.Log("[Profile Revealer] Did not find Tweaks service.");
						LeaderboardController.Install();
					}
				}
			}
			this.gameState = state;
		}

		private void SceneManager_sceneLoaded(Scene scene, LoadSceneMode loadSceneMode) {
			if (scene.name == "gameplayLoadingScene") {
				if (this.config.IsAdvantagusFeatures && GameplayState.MissionToLoad != FreeplayMissionGenerator.FREEPLAY_MISSION_ID &&
					GameplayState.MissionToLoad != ModMission.CUSTOM_MISSION_ID)
					this.StartCoroutine(this.ShowAdvantageousWarning());
			}
		}
#endif

		private void RefreshConfig() {
			bool rewriteFile;
			try {
				this.config = JsonConvert.DeserializeObject<Config>(this.KMModSettings.Settings);
				if (this.config != null) {
					// Make sure that the config file uses the current format; otherwise the Tweaks settings page does not initialise properly.
					var dictionary = JsonConvert.DeserializeObject<IDictionary<string, object>>(this.KMModSettings.Settings);
					rewriteFile = !dictionary.ContainsKey(nameof(Config.PopupKeys));
				} else {
					this.config = new Config();
					rewriteFile = true;
				}
			} catch (JsonSerializationException ex) {
				Debug.LogError("[Profile Revealer] The mod settings file is invalid.");
				Debug.LogException(ex, this);
				this.config = new Config();
				rewriteFile = true;
			}
			if (rewriteFile) {
				using var writer2 = new StreamWriter(this.KMModSettings.SettingsPath);
				new JsonSerializer() { Formatting = Formatting.Indented }.Serialize(writer2, this.config);
			}
			// Respect Disable Advantageous Features in Tweaks.
			if (this.config.IsAdvantagusFeatures && this.tweaksService != null) {
				if (this.tweaksSettingsField == null)
					this.tweaksSettingsField = this.tweaksService.GetType().GetField("settings", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
				var tweaksSettings = this.tweaksSettingsField.GetValue(null);
				if (this.tweaksDisableAdvantageousField == null)
					this.tweaksDisableAdvantageousField = tweaksSettings.GetType().GetField("DisableAdvantageous", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
				if ((bool) this.tweaksDisableAdvantageousField.GetValue(tweaksSettings)) {
					Debug.LogWarning("[Profile Revealer] Advantageous features are disabled in Tweaks settings. Overriding Show Module Names setting.");
					this.config.ShowModuleNames = false;
					this.config.ShowBossStatus = false;
				}
			}
		}

		private IEnumerator ShowAdvantageousWarning() {
			yield return null;
			// If Tweaks is present, we'll use its warning.
			if (this.tweaksService != null) {
				var warning = this.tweaksService.transform.Find("UI/AdvantageousWarning");
				if (warning == null || !warning.gameObject.activeSelf) {
					this.StartCoroutine((IEnumerator) this.tweaksService.GetType().GetMethod("ShowAdvantageousWarning", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
						.Invoke(this.tweaksService, null));
				}
			} else {
				// Otherwise use our own.
				this.AdvantageousWarningCanvas.SetActive(true);
				yield return new WaitForSeconds(5);
				this.AdvantageousWarningCanvas.SetActive(false);
			}
		}

#if !DEBUG
		private IEnumerator CheckForBombs() {
			Debug.Log("[Profile Revealer] Waiting for bombs...");
			var oldBombs = new List<Bomb>();
			var bombs = SceneManager.Instance.GameplayState.Bombs;
			var count = 0;
			while (bombs.Count == 0) yield return null;
			// Wait to see if any more bombs are being generated.
			while (true) {
				count = bombs.Count;
				yield return new WaitForSeconds(0.5f);
				if (bombs.Count == count) break;
			}
			Debug.Log($"[Profile Revealer] Found {bombs.Count} bomb(s).");

			var isFactoryRoom = bombs[0].GetComponent<Selectable>().Parent.name.StartsWith("FactoryRoom");

			// Disable leaderboards (needs to be done now to override Tweaks).
			if (this.config.IsAdvantagusFeatures) {
				Assets.Scripts.Stats.StatsManager.Instance.DisableStatChanges = true;
				Assets.Scripts.Records.RecordManager.Instance.DisableBestRecords = true;
			}

			// Load profiles.
			var profiles = new List<KeyValuePair<string, HashSet<string>>>();
			var inactiveVetos = new List<KeyValuePair<string, HashSet<string>>>();
			var enabledProfiles = new HashSet<string>();
			var path = Path.Combine(Application.persistentDataPath, "modSelectorConfig.json");
			if (File.Exists(path)) {
				using var reader = new StreamReader(path);
				enabledProfiles = new JsonSerializer().Deserialize<HashSet<string>>(new JsonTextReader(reader));
			}
			path = Path.Combine(Application.persistentDataPath, "ModProfiles");
			if (Directory.Exists(path)) {
				foreach (var file in Directory.GetFiles(path, "*.json")) {
					var profileName = Path.GetFileNameWithoutExtension(file);
					try {
						using var reader = new StreamReader(file);
						var profile = new JsonSerializer().Deserialize<Profile>(new JsonTextReader(reader));
						if (profile.DisabledList == null) {
							Debug.LogWarning($"[Profile Revealer] Could not load profile {Path.GetFileName(file)}");
							Debug.LogWarning($"{nameof(profile.DisabledList)} is missing.");
							continue;
						}
						if (enabledProfiles.Contains(profileName)) {
							if (profile.Operation == ProfileType.Expert)
								profiles.Add(new KeyValuePair<string, HashSet<string>>(profileName, profile.DisabledList));
						} else {
							if (profile.Operation != ProfileType.Expert)
								inactiveVetos.Add(new KeyValuePair<string, HashSet<string>>(profileName, profile.DisabledList));
						}
					} catch (Exception ex) {
						Debug.LogWarning($"[Profile Revealer] Could not load profile {Path.GetFileName(file)}");
						Debug.LogException(ex, this);
					}
				}
			} else
				Debug.Log($"[Profile Revealer] The Mod Selector profile directory does not exist.");

			Debug.Log($"[Profile Revealer] Looking for Dynamic Mission Generator API.");
			var dynamicMissionGeneratorService = GameObject.Find("Dynamic Mission Generator API");
			var dynamicMissionGeneratorApi = dynamicMissionGeneratorService?.GetComponent<IDictionary<string, IDictionary<string, IList<string>>>>();
			this.moduleProfiles = dynamicMissionGeneratorApi != null ? dynamicMissionGeneratorApi["ModuleProfiles"] : null;
			var bombIndex = 0;

			var instanceCount = new Dictionary<string, int>();

			while (true) {
				foreach (var bomb in bombs.Except(oldBombs)) {
					var moduleIndex = 0;
					foreach (var component in bomb.BombComponents) {
						if (component.ComponentType == ComponentTypeEnum.Empty || component.ComponentType == ComponentTypeEnum.Timer) continue;
						Debug.Log($"[Profile Revealer] Attaching to '{component.name}'.");

						var kmBombModule = component.GetComponent<KMBombModule>();
						var kmNeedyModule = component.GetComponent<KMNeedyModule>();

						var popup = Instantiate(this.PopupPrefab, component.transform, false);
						popup.Module = component.transform;
						++moduleIndex;
						if (this.config.ShowModuleNames) popup.moduleName = component.GetModuleDisplayName();
						if (this.config.ShowBossStatus && bossStatus.ContainsKey(component.GetModuleDisplayName()))
						{
							popup.bossStatus = bossStatus[component.GetModuleDisplayName()];
						}

						popup.Delay = this.config.Delay;
						if (kmBombModule == null && kmNeedyModule == null) {
							// Vanilla modules will be shown as enabled by all profiles.
							// Otherwise it could be used to easily find the two vanilla modules on the Centurion, for instance.
							popup.enabledProfiles = profiles.Select(p => p.Key);
						} else {
							var moduleID = kmBombModule != null ? kmBombModule.ModuleType : kmNeedyModule.ModuleType;
							popup.enabledProfiles = profiles.Where(p => !p.Value.Contains(moduleID)).Select(p => p.Key);
							popup.disabledProfiles = profiles.Where(p => p.Value.Contains(moduleID)).Select(p => p.Key);
							popup.inactiveProfiles = inactiveVetos.Where(p => p.Value.Contains(moduleID)).Select(p => p.Key);

							if (moduleProfiles != null && moduleProfiles.TryGetValue(moduleID, out var list)) {
								if (!instanceCount.TryGetValue(moduleID, out var index)) index = 0;
								popup.ProfileName = index < list.Count ? list[index] : null;
								instanceCount[moduleID] = index + 1;
							}
						}
						this.popups.Add(popup);

						if (!KTInputManager.Instance.IsMotionControlMode()) {
							var selectable = component.GetComponent<Selectable>();
							selectable.OnHighlight += () => { this.highlightedModulePopup = popup; this.SetProfileName(popup); popup.ShowDelayed(); };
							selectable.OnHighlightEnded += () => { if (this.highlightedModulePopup == popup) this.highlightedModulePopup = null; popup.Hide(); };
							selectable.OnFocus += () => { this.focusedModulePopup = popup; popup.Hide(); };
							selectable.OnDefocus += () => { if (this.focusedModulePopup == popup) this.focusedModulePopup = null; popup.Hide(); };
						}
					}
					oldBombs.Add(bomb);
					++bombIndex;
				}

				if (!isFactoryRoom) yield break;
				Debug.Log("[Profile Revealer] Factory room is active. Waiting for new bombs.");
				while (!bombs.Except(oldBombs).Any()) {
					if (this.gameState != KMGameInfo.State.Gameplay) yield break;
					yield return new WaitForSeconds(1);
				}
			}
		}
#endif

		[Obsolete("No longer needed")]
		private void SetProfileName(ModulePopup popup) {
			//popup.ProfileName = this.moduleProfiles != null && this.moduleProfiles.TryGetValue(popup.Module.gameObject, out var profile) ? profile : null;
		}

		private IEnumerator CheckForBombsTest() {
			Debug.Log("[Profile Revealer] Looking for bombs.");
			KMBomb[] bombs;
			while (true) {
				bombs = FindObjectsOfType<KMBomb>();
				if (bombs.Length > 0) break;
				yield return null;
			}
			foreach (var bomb in bombs) {
				foreach (var transform in bomb.transform.Find("Modules").Cast<Transform>()) {
					string name;
					var module = transform.GetComponent<KMBombModule>();
					if (module != null) name = module.ModuleDisplayName;
					else {
						var needyModule = transform.GetComponent<KMNeedyModule>();
						if (needyModule != null) name = needyModule.ModuleDisplayName;
						else continue;
					}
					Debug.Log($"[Profile Revealer] Attaching to '{name}'.");

					var popup = Instantiate(this.PopupPrefab, transform, false);
					popup.Module = transform;
					if (this.config.ShowModuleNames) popup.moduleName = transform.name;
					if (this.config.ShowBossStatus && bossStatus.ContainsKey(transform.name))
					{
						popup.bossStatus = bossStatus[transform.name];
					}
					popup.Delay = 2;
					popup.enabledProfiles = new[] { "Alice", "Bob" };
					popup.disabledProfiles = new[] { "Carol", "Dan" };
					popup.inactiveProfiles = new[] { "Veto A", "Veto B" };
					var selectable = transform.GetComponent<KMSelectable>();
					selectable.OnHighlight += () => { this.highlightedModulePopup = popup; popup.ShowDelayed(); };
					selectable.OnHighlightEnded += () => { if (this.highlightedModulePopup == popup) this.highlightedModulePopup = null; popup.Hide(); };
					selectable.OnFocus += () => { this.focusedModulePopup = popup; popup.Hide(); };
					selectable.OnDefocus += () => { if (this.focusedModulePopup == popup) this.focusedModulePopup = null; popup.Hide(); };
				}
			}
			yield break;
		}

#pragma warning disable CS0649  // Field is never assigned to
		private struct Profile {
			public HashSet<string> DisabledList;
			public ProfileType Operation;
		}

		private enum ProfileType {
			Expert,
			Defuser
		}
	}
}
