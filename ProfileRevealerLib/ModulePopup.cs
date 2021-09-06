using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace ProfileRevealerLib {
	public class ModulePopup : MonoBehaviour {
		public RectTransform Canvas;
		public Text Text;
		public RectTransform BoxCollider;

		public bool Visible => this.Canvas.gameObject.activeSelf;

		public float Delay { get; set; }
		public Transform Module { get; set; }
		public string ProfileName { get => this.profileName; set { this.profileName = value; this.setText(); } }
		internal IList<string> bossStatus;

		internal string moduleName;
		private string profileName;
		internal IEnumerable<string> enabledProfiles;
		internal IEnumerable<string> disabledProfiles;
		internal IEnumerable<string> inactiveProfiles;
		private Coroutine coroutine;

		public void Start() {
			this.Canvas.gameObject.SetActive(false);

			this.setText();

			var colliders = new List<Collider>();
			foreach (var collider in this.Module.GetComponentsInChildren<Collider>(true)) {
				if (!collider.enabled) {
					colliders.Add(collider);
					collider.enabled = true;
				}
			}

			var halfExtents = this.BoxCollider.transform.lossyScale;
			halfExtents.Scale(new Vector3(300, 150, 100));
			if (Physics.CheckBox(this.BoxCollider.position, halfExtents, this.BoxCollider.rotation)) {
				var transform = (RectTransform) this.Canvas.GetChild(0);
				transform.pivot = new Vector2(0, 1);
			}

			foreach (var collider in colliders) collider.enabled = false;
		}

		private void setText() {
			var enabledProfilesStr = Join(", ", this.enabledProfiles);
			var disabledProfilesStr = Join(", ", this.disabledProfiles);
			var inactiveProfilesStr = Join(", ", this.inactiveProfiles);

			var builder = new StringBuilder();
			if (this.moduleName != null) builder.AppendLine($"<b>{this.moduleName}</b>");
			if (this.bossStatus != null) builder.AppendLine($"<color=red>({ String.Join(", ", this.bossStatus.ToArray())})</color>");
			if (ProfileName != null)
				builder.AppendLine($"Chosen from: <color=yellow>{ProfileName}</color>");
			if (enabledProfilesStr.Length > 0)
				builder.AppendLine($"Enabled by: <color=lime>{enabledProfilesStr}</color>");
			if (disabledProfilesStr.Length > 0)
				builder.AppendLine($"Disabled by: <color=red>{disabledProfilesStr}</color>");
			if (inactiveProfilesStr.Length > 0)
				builder.AppendLine($"Inactive vetos: <color=silver>{inactiveProfilesStr}</color>");
			if (builder.Length == 0) this.Text.text = "No profiles found.";
			else {
				builder.Remove(builder.Length - 1, 1);
				this.Text.text = builder.ToString();
			}
		}

		private static string Join<T>(string separator, IEnumerable<T> enumerable) {
			if (enumerable == null) return "";
			var enumerator = enumerable.GetEnumerator();
			if (!enumerator.MoveNext()) return "";
			var builder = new StringBuilder();
			builder.Append(enumerator.Current);
			while (enumerator.MoveNext()) {
				builder.Append(separator);
				builder.Append(enumerator.Current);
			}
			return builder.ToString();
		}

		public void ShowDelayed() {
			if (this.coroutine != null) this.StopCoroutine(this.coroutine);
			this.coroutine = this.StartCoroutine(this.DelayCoroutine());
		}

		public void Show() {
			if (this.coroutine != null) this.StopCoroutine(this.coroutine);
			this.coroutine = null;
			this.Canvas.gameObject.SetActive(true);
		}

		public void Hide() {
			if (this.coroutine != null) this.StopCoroutine(this.coroutine);
			this.coroutine = null;
			this.Canvas.gameObject.SetActive(false);
		}

		private IEnumerator DelayCoroutine() {
			if (this.Delay < 0 || float.IsInfinity(this.Delay) || float.IsNaN(this.Delay)) yield break;
			yield return new WaitForSeconds(this.Delay);
			this.Canvas.gameObject.SetActive(true);
		}
	}
}
