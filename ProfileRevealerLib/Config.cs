using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;

namespace ProfileRevealerLib {
	public partial class Config {
		public bool ShowModuleNames;
		public bool ShowBossStatus = true;
		public float Delay = 2;
		[JsonIgnore]
		public KeyCode PopupKey = KeyCode.F1;
		[JsonIgnore]
		public KMGamepad.ButtonEnum PopupButton;
		[JsonIgnore]
		public KMGamepad.AxisEnum PopupAxis;
		[JsonIgnore]
		public ModifierKeys PopupKeyModifiers;

		public string PopupKeys {
			get {
				var builder = new StringBuilder();
				if ((this.PopupKeyModifiers & ModifierKeys.Ctrl) != 0) builder.Append("Ctrl+");
				if ((this.PopupKeyModifiers & ModifierKeys.Command) != 0) builder.Append("Command+");
				if ((this.PopupKeyModifiers & ModifierKeys.Super) != 0) builder.Append("Super+");
				if ((this.PopupKeyModifiers & ModifierKeys.Shift) != 0) builder.Append("Shift+");
				if ((this.PopupKeyModifiers & ModifierKeys.Alt) != 0) builder.Append("Alt+");
				if (this.PopupKey != 0) builder.Append(this.PopupKey);
				else if (this.PopupButton >= 0) builder.Append("Gamepad" + this.PopupButton);
				else builder.Append("Gamepad" + this.PopupAxis);
				return builder.ToString();
			}
			set {
				var tokens = value.Split('+');
				this.PopupKeyModifiers = 0;
				for (int i = tokens.Length - 2; i >= 0; --i) {
					ModifierKeys modifier;
					switch (tokens[i].ToLowerInvariant()) {
						case "shift": case "s": modifier = ModifierKeys.Shift; break;
						case "ctrl": case "control": case "c": modifier = ModifierKeys.Ctrl; break;
						case "alt": case "a": modifier = ModifierKeys.Alt; break;
						case "command": case "cmd": modifier = ModifierKeys.Command; break;
						case "super": case "win": case "w": modifier = ModifierKeys.Super; break;
						default: throw new FormatException($"Unknown modifier key '{tokens[i]}'.");
					}
					this.PopupKeyModifiers |= modifier;
				}
				string button = tokens[tokens.Length - 1];
				if (button.StartsWith("Gamepad", StringComparison.InvariantCultureIgnoreCase)) {
					this.PopupKey = 0;
					button = button.Substring(7);
					if (button.Equals("LT", StringComparison.InvariantCultureIgnoreCase)) {
						this.PopupButton = (KMGamepad.ButtonEnum) (-1);
						this.PopupAxis = KMGamepad.AxisEnum.LT;
					} else if (button.Equals("RT", StringComparison.InvariantCultureIgnoreCase)) {
						this.PopupButton = (KMGamepad.ButtonEnum) (-1);
						this.PopupAxis = KMGamepad.AxisEnum.RT;
					} else
						this.PopupButton = (KMGamepad.ButtonEnum) Enum.Parse(typeof(KMGamepad.ButtonEnum), button, true);
				} else
					this.PopupKey = (KeyCode) Enum.Parse(typeof(KeyCode), button, true);
			}
		}

		[JsonIgnore]
		public bool IsAdvantagusFeatures
		{
			get
			{
				return ShowModuleNames || ShowBossStatus;

			}
		}

		internal static readonly Dictionary<string, object>[] TweaksEditorSettings = new[] {
			new Dictionary<string, object> {
				{ "Filename", "ProfileRevealer-settings.txt" },
				{ "Name", "Profile Revealer" },
				{ "Listings", new List<Dictionary<string, object>> {
						new Dictionary<string, object> { { "Key", nameof(ShowModuleNames) }, { "Text", "Show Module Names" }, { "Description", "Shows the name of the module in the popup. Disables leaderboards.\nDisable Advantageous Features in Tweaks overrides this." } },
						new Dictionary<string, object> { { "Key", nameof(ShowBossStatus) }, { "Text", "Show Boss Status" }, { "Description", "Shows the boss status of the module in the popup if \"Show Module Names\" is enabled." } },
						new Dictionary<string, object> { { "Key", nameof(Delay) }, { "Text", "Popup Delay" }, { "Description", "Time in seconds a module should be higlighted before showing the popup." } },
						new Dictionary<string, object> { { "Key", nameof(PopupKeys) }, { "Text", "Popup Keys" }, { "Description", "The button which can be used to show the popup.\ne.g. F1, Ctrl+F1, Command+Shift+Space, Mouse2, GamepadLB." } }
					}
				}
			}
		};
	}
}
