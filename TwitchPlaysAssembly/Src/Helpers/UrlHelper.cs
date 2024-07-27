using System;
using System.IO;
using System.Linq;
using UnityEngine;

public class UrlHelper : MonoBehaviour
{
	public static UrlHelper Instance;

	private void Awake() => Instance = this;

	public static void ChangeMode(bool toShort)
	{
		TwitchPlaySettings.data.LogUploaderShortUrls = toShort;
		TwitchPlaySettings.WriteDataToFile();
	}

	public static bool ToggleMode()
	{
		TwitchPlaySettings.data.LogUploaderShortUrls = !TwitchPlaySettings.data.LogUploaderShortUrls;
		TwitchPlaySettings.WriteDataToFile();
		return TwitchPlaySettings.data.LogUploaderShortUrls;
	}

	public static string LogAnalyserFor(string url) => string.Format(TwitchPlaySettings.data.AnalyzerUrl + "#url={0}", url);

	public static string CommandReference => TwitchPlaySettings.data.LogUploaderShortUrls ? "https://tinyurl.com/v3twx5a" : "https://samfundev.github.io/KtaneTwitchPlays";

	public static string ManualFor(string moduleEN, string moduleJA, string author, string type = "html", bool useVanillaRuleModifier = false) {
		string[] VanillaJPID = {"ワイヤ", "キーパッド", "ボタン", "コンデンサー", "複雑ワイヤ", "ダイヤル", "迷路", "記憶", "モールス信号", "パスワード", "サイモンゲーム", "表比較", "順番ワイヤ" };
		string[] VanillaENID= { "Wires", "Keypad", "BigButton", "NeedyCapacitor", "Venn", "NeedyKnob", "Maze", "Memory", "Morse", "Password", "Simon", "WhosOnFirst", "WireSequence" };
   
   if(Array.IndexOf(VanillaJPID, moduleEN) != -1) {return string.Format(TwitchPlaySettings.data.RepositoryUrl + "{0}/{1}.{2}{3}", type.ToUpper(), NameToUrl(VanillaENID[Array.IndexOf(VanillaJPID, moduleEN)] + " translated (日本語 — " + moduleJA + ")"), type, (useVanillaRuleModifier && type.Equals("html")) ? $"#{VanillaRuleModifier.GetRuleSeed()}" : "");}
   else{
  return string.Format(TwitchPlaySettings.data.RepositoryUrl + "{0}/{1}.{2}{3}", type.ToUpper(), moduleEN!=moduleJA ? NameToUrl(moduleEN + " translated (日本語 — " + moduleJA + ") " + author) : NameToUrl(moduleEN), type, (useVanillaRuleModifier && type.Equals("html")) ? $"#{VanillaRuleModifier.GetRuleSeed()}" : "");
   }
  }

	// 例：https://ktane.timwi.de/HTML/Plumbing%20translated%20(日本語%20—%20配管)%20(hatosable).html

	public static string ManualFor(string module) => string.Format(module);
	// 例：moduleinformation.jsonにあるやつを読む

	public static string MissionLink(string mission) => "https://bombs.samfun.dev/mission/" + TwitchUrlEscape(mission);

	private static string TwitchUrlEscape(string name) => Uri.EscapeDataString(Uri.UnescapeDataString(name)).Replace("*", "%2A").Replace("!", "%21").Replace("'", "%27");
	
	private static string NameToUrl(string name) => Uri.EscapeDataString(Uri.UnescapeDataString(name).Replace("'", "’").Split(InvalidCharacters).Join("")).Replace("*", "%2A").Replace("!", "%21").Replace("'", "%27");

	private static readonly char[] InvalidCharacters = Path.GetInvalidFileNameChars().Where(c => c != '*').ToArray();
}
