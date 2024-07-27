using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine;

using Random = UnityEngine.Random;

/// <summary>いつでも使えるコマンド</summary>
static class GlobalCommands
{
	/// <name>Help</name>
	/// <syntax>help</syntax>
	/// <summary>TPのやり方について説明する。</summary>
	[Command(@"(help)")]
	public static void Help(string user, bool isWhisper)
	{
		string[] alphabet = new string[26] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z" };
		string[] randomCodes =
		{
			TwitchPlaySettings.data.EnableLetterCodes ? alphabet[Random.Range(0, alphabet.Length)] + alphabet[Random.Range(0, alphabet.Length)] : Random.Range(1, 100).ToString(),
			TwitchPlaySettings.data.EnableLetterCodes ? alphabet[Random.Range(0, alphabet.Length)] + alphabet[Random.Range(0, alphabet.Length)] : Random.Range(1, 100).ToString()
		};

		IRCConnection.SendMessage(string.Format("!{0} manual [マニュアルへのリンク] | マニュアルは {1}?lang=ja から探すことができます。", randomCodes[0], TwitchPlaySettings.data.RepositoryUrl), user, !isWhisper);
		IRCConnection.SendMessage(string.Format("!{0} help [モジュールのコマンド] | コマンドについては https://tepel-chen.github.io/tpCommands/ を参照してください。", randomCodes[1], UrlHelper.CommandReference), user, !isWhisper);
	}

	/// <name>Date</name>
	/// <syntax>date</syntax>
	/// <summary>現在の日時を表示する。</summary>
	[Command(@"(date|time)")]
	public static void CurrentTime(string user, bool isWhisper)
	{
		IRCConnection.SendMessage(string.Format("現在の日時：{0}, {1}", DateTime.Now.ToString("MM月dd日"), DateTime.Now.ToString("HH:mm:ss"), !isWhisper));
	}

	/// <name>Manual</name>
	/// <syntax>manual [module]</syntax>
	/// <summary>特定のモジュールのマニュアルを表示する。</summary>
	[Command(@"(manual) (\S+)")]
	public static void Manual([Group(1)] string moduleName, string user, bool isWhisper)
	{
		bool valid = ComponentSolverFactory.GetModuleInformation().Search(moduleName, x => x.moduleDisplayName, x => $"“{x.moduleDisplayName}”", out ModuleInformation result, out string message);

		if (valid)
			IRCConnection.SendMessage($"{result.moduleDisplayName}のマニュアル：{UrlHelper.ManualFor(result.manualCode)}", user, !isWhisper);
		else
			IRCConnection.SendMessage(message, user, !isWhisper);
	}

	/// <name>Bonus Points</name>
	/// <syntax>bonuspoints [player] [points]</syntax>
	/// <summary>プレイヤーに手動でポイントを追加する。</summary>
	/// <restriction>Admin</restriction>
	[Command(@"bonus(?:score|points) (\S+) (-?[0-9]+)", AccessLevel.Admin, AccessLevel.Admin)]
	public static void BonusPoints([Group(1)] string targetPlayer, [Group(2)] int bonus, string user)
	{
		targetPlayer = targetPlayer.FormatUsername();
		IRCConnection.SendMessageFormat(TwitchPlaySettings.data.GiveBonusPoints, targetPlayer, bonus, user);
		Leaderboard.Instance.AddScore(targetPlayer, new Color(.31f, .31f, .31f), bonus);
	}

	/// <name>Bonus Solves</name>
	/// <syntax>bonussolves [player] [solves]</syntax>
	/// <summary>プレイヤーに手動で解除数を追加する。</summary>
	/// <restriction>Admin</restriction>
	[Command(@"bonussolves? (\S+) (-?[0-9]+)", AccessLevel.Admin, AccessLevel.Admin)]
	public static void BonusSolves([Group(1)] string targetPlayer, [Group(2)] int bonus, string user)
	{
		targetPlayer = targetPlayer.FormatUsername();
		IRCConnection.SendMessageFormat(TwitchPlaySettings.data.GiveBonusSolves, targetPlayer, bonus, user);
		Leaderboard.Instance.AddSolve(targetPlayer, new Color(.31f, .31f, .31f), bonus);
	}

	/// <name>Bonus Strikes</name>
	/// <syntax>bonusstrikes [player] [strikes]</syntax>
	/// <summary>プレイヤーに手動でミスを追加する。</summary>
	/// <restriction>Admin</restriction>
	[Command(@"bonusstrikes? (\S+) (-?[0-9]+)", AccessLevel.Admin, AccessLevel.Admin)]
	public static void BonusStrikes([Group(1)] string targetPlayer, [Group(2)] int bonus, string user)
	{
		targetPlayer = targetPlayer.FormatUsername();
		IRCConnection.SendMessageFormat(TwitchPlaySettings.data.GiveBonusStrikes, targetPlayer, bonus, user);
		Leaderboard.Instance.AddStrike(targetPlayer, new Color(.31f, .31f, .31f), bonus);
	}

	/// <name>Strike Refund</name>
	/// <syntax>srefund [user] (count)</syntax>
	/// <summary>プレイヤーのミスを取り消す。</summary>
	/// <restriction>Admin</restriction>
	[Command(@"srefund +(\S+) *?( +[0-9]+)?", AccessLevel.Admin, AccessLevel.Admin)]
	public static void StrikeRefund([Group(1)] string targetPlayer, [Group(2)] int? _count, string user)
	{
		int count = _count ?? 1;
		targetPlayer = targetPlayer.FormatUsername();
		if (count < 1)
		{
			IRCConnection.SendMessageFormat("@{0}さん：取り消しするミスの数は1以上でなければなりません。", user);
			return;
		}

		int points = TwitchPlaySettings.data.StrikePenalty * count;
		Leaderboard.Instance.AddStrike(targetPlayer, new Color(.31f, .31f, .31f), -1 * count);
		Leaderboard.Instance.AddScore(targetPlayer, new Color(.31f, .31f, .31f), points);

		IRCConnection.SendMessageFormat("@{2}から{0}回のミスと{1}ポイントの減点を取り消しました。", count, points, targetPlayer);
	}

	/// <name>Strike Transfer</name>
	/// <syntax>stransfer [user] to [user] (count)</syntax>
	/// <summary>プレイヤー1からプレイヤー2へミスを移動させる。</summary>
	/// <restriction>Admin</restriction>
	[Command(@"stransfer +(\S+) +to +(\S+) *?( +[0-9]+)?", AccessLevel.Admin, AccessLevel.Admin)]
	public static void StrikeTransfer([Group(1)] string fromPlayer, [Group(2)] string toPlayer, [Group(3)] int? _count, string user)
	{
		int count = _count ?? 1;
		fromPlayer = fromPlayer.FormatUsername();
		toPlayer = toPlayer.FormatUsername();

		if (count < 1)
		{
			IRCConnection.SendMessageFormat("@{0}さん：移行するミスの数は1以上でなければなりません。", user);
			return;
		}

		int points = TwitchPlaySettings.data.StrikePenalty * count;
		Leaderboard.Instance.AddStrike(fromPlayer, new Color(.31f, .31f, .31f), -1 * count);
		Leaderboard.Instance.AddScore(fromPlayer, new Color(.31f, .31f, .31f), points);
		Leaderboard.Instance.AddStrike(toPlayer, new Color(.31f, .31f, .31f), count);
		Leaderboard.Instance.AddScore(toPlayer, new Color(.31f, .31f, .31f), -1 * points);

		IRCConnection.SendMessageFormat("@{2}から{3}へ、{0}回のミスと{1}ポイントの減点を移行しました。", count, points, fromPlayer, toPlayer);
	}

	/// <name>Set Reward</name>
	/// <syntax>reward [points]</syntax>
	/// <summary>爆弾解除の報酬を設定する。</summary>
	/// <restriction>Admin</restriction>
	[Command(@"reward (-?[0-9]+)", AccessLevel.Admin, AccessLevel.Admin)]
	public static void SetReward([Group(1)] int reward) => TwitchPlaySettings.SetRewardBonus(reward);

	/// <name>Add Reward</name>
	/// <syntax>bonusreward [points]</syntax>
	/// <summary>爆弾解除の報酬を追加する。</summary>
	/// <restriction>Admin</restriction>
	[Command(@"bonusreward (-?[0-9]+)", AccessLevel.Admin, AccessLevel.Admin)]
	public static void AddReward([Group(1)] int reward) => TwitchPlaySettings.AddRewardBonus(reward);

	/// <name>Time Mode</name>
	/// <syntax>timemode [state]</syntax>
	/// <summary>タイムモードをオン/オフにする。</summary>
	[Command(@"timemode( *(on)| *off)?")]
	public static void TimeMode([Group(1)] bool any, [Group(2)] bool on, string user, bool isWhisper) => SetGameMode(TwitchPlaysMode.Time, !any, on, user, isWhisper, TwitchPlaySettings.data.EnableTimeModeForEveryone, TwitchPlaySettings.data.TimeModeCommandDisabled);
	/// <name>VS Mode</name>
	/// <syntax>vsmode [state]</syntax>
	/// <summary>VSモードをオン/オフにする。</summary>
	/// <restriction>Mod</restriction>
	[Command(@"vsmode( *(on)| *off)?", AccessLevel.Mod, AccessLevel.Mod)]
	public static void VsMode([Group(1)] bool any, [Group(2)] bool on, string user, bool isWhisper) => SetGameMode(TwitchPlaysMode.VS, !any, on, user, isWhisper, false, TwitchPlaySettings.data.VsModeCommandDisabled);
	/// <name>Zen Mode</name>
	/// <syntax>zenmode [state]</syntax>
	/// <summary>禅モードをオン/オフにする。</summary>
	[Command(@"zenmode( *(on)| *off)?")]
	public static void ZenMode([Group(1)] bool any, [Group(2)] bool on, string user, bool isWhisper) => SetGameMode(TwitchPlaysMode.Zen, !any, on, user, isWhisper, TwitchPlaySettings.data.EnableZenModeForEveryone, TwitchPlaySettings.data.ZenModeCommandDisabled);
	/// <name>Training Mode</name>
	/// <syntax>trainingmode [state]</syntax>
	/// <summary>トレーニングモードをオン/オフにする。</summary>
	[Command(@"trainingmode( *(on)| *off)?")]
	public static void TrainingMode([Group(1)] bool any, [Group(2)] bool on, string user, bool isWhisper) => SetGameMode(TwitchPlaysMode.Training, !any, on, user, isWhisper, TwitchPlaySettings.data.EnableTrainingModeForEveryone, TwitchPlaySettings.data.TrainingModeCommandDisabled);

	/// <name>Show Mode</name>
	/// <syntax>mode</syntax>
	/// <summary>現在および次のゲームモードを表示する。</summary>
	[Command(@"modes?")]
	public static void ShowMode(string user, bool isWhisper)
	{
		IRCConnection.SendMessage(string.Format("現在は{0}モードが有効化されています。次のゲームは{1}モードになります。", OtherModes.GetName(OtherModes.currentMode), OtherModes.GetName(OtherModes.nextMode)), user, !isWhisper);
		if (TwitchPlaySettings.data.AnarchyMode)
			IRCConnection.SendMessage("現在アナーキーモードです。", user, !isWhisper);
	}

	/// <name>Reset User</name>
	/// <syntax>resetuser [users]</syntax>
	/// <summary>リーダーボード上の特定ユーザー情報を削除する。</summary>
	/// <restriction>SuperUser</restriction>
	[Command(@"resetusers? +(.+)", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static void ResetUser([Group(1)] string parameters, string user, bool isWhisper)
	{
		foreach (string userRaw in parameters.Split(';'))
		{
			string usertrimmed = userRaw.Trim();
			Leaderboard.Instance.GetRank(usertrimmed, out var entry);
			Leaderboard.Instance.GetSoloRank(usertrimmed, out var soloEntry);
			if (entry == null && soloEntry == null)
			{
				IRCConnection.SendMessage($"ユーザー{usertrimmed}はすでにリセットされたか、あるいは見つかりませんでした。", user, !isWhisper);
				continue;
			}
			if (entry != null)
				Leaderboard.Instance.DeleteEntry(entry);
			if (soloEntry != null)
				Leaderboard.Instance.DeleteSoloEntry(soloEntry);
			IRCConnection.SendMessage($"ユーザー{usertrimmed}はリセットされました。", userRaw, !isWhisper);
		}
	}

	#region Voting
	/// <name>Start a vote</name>
	/// <syntax>vote [action]</syntax>
	/// <summary>投票を開始する</summary>
	[Command(@"vote (togglevs)")]
	public static void VoteStart(string user, [Group(1)] bool VSMode) => Votes.StartVote(user, VSMode ? VoteTypes.VSModeToggle : 0);

	/// <name>Vote</name>
	/// <syntax>vote [choice]</syntax>
	/// <summary>はい/いいえで投票を行う。</summary>
	[Command(@"vote ((yes|voteyea)|(no|votenay))")]
	public static void Vote(string user, [Group(2)] bool yesVote) => Votes.Vote(user, yesVote);

	/// <name>Remove vote</name>
	/// <syntax>vote remove</syntax>
	/// <summary>投票を削除する。</summary>
	[Command(@"vote remove")]
	public static void RemoveVote(string user) => Votes.RemoveVote(user);

	/// <name>Time left of vote</name>
	/// <syntax>vote time</syntax>
	/// <summary>残り投票可能時間を表示する。</summary>
	[Command(@"vote time")]
	public static void ShowVoteTime(string user) => Votes.TimeLeftOnVote(user);

	/// <name>Cancel vote</name>
	/// <syntax>vote cancel</syntax>
	/// <summary>投票をキャンセルする。</summary>
	/// <restriction>Mod</restriction>
	[Command(@"vote cancel", AccessLevel.Mod, AccessLevel.Mod)]
	public static void CancelVote(string user) => Votes.CancelVote(user);

	/// <name>Force-end vote</name>
	/// <syntax>vote forceend</syntax>
	/// <summary>投票を強制的に締め切る。</summary>
	/// <restriction>Mod</restriction>
	[Command(@"vote forceend", AccessLevel.Mod, AccessLevel.Mod)]
	public static void ForceEndVote(string user) => Votes.EndVoteEarly(user);
	#endregion

	/// <name>My Rank</name>
	/// <syntax>rank</syntax>
	/// <summary>現在のランクを表示する。</summary>
	[Command(@"rank")]
	public static void OwnRank(string user, bool isWhisper) { Leaderboard.Instance.GetRank(user, out var entry); ShowRank(entry, user, user, isWhisper); }

	/// <name>Get Solo Rank</name>
	/// <syntax>rank solo [rank]</syntax>
	/// <summary>現在のソロプレイにおけるランクを表示する。</summary>
	[Command(@"rank solo (\d+)")]
	public static void SoloRank([Group(1)] int desiredRank, string user, bool isWhisper)
	{
		var entries = Leaderboard.Instance.GetSoloEntries(desiredRank);
		ShowRank(entries, user, user, isWhisper, numeric: true);
	}

	/// <name>Get Solo Rank By User</name>
	/// <syntax>rank solo [user]</syntax>
	/// <summary>特定ユーザーのソロプレイにおけるランクを表示する。</summary>
	[Command(@"rank solo (?!\d+$)(.*)")]
	public static void SoloRankByUser([Group(1)] string desiredUser, string user, bool isWhisper) { Leaderboard.Instance.GetSoloRank(desiredUser, out var entry); ShowRank(entry, desiredUser, user, isWhisper); }

	/// <name>Get Rank</name>
	/// <syntax>rank [rank]</syntax>
	/// <summary>特定ランクのユーザーを表示する。</summary>
	[Command(@"rank (\d+)")]
	public static void Rank([Group(1)] int desiredRank, string user, bool isWhisper)
	{
		var entries = Leaderboard.Instance.GetEntries(desiredRank);
		ShowRank(entries, user, user, isWhisper, numeric: true);
	}

	/// <name>Get Rank By User</name>
	/// <syntax>rank [user]</syntax>
	/// <summary>>特定ユーザーのランクを表示する。</summary>
	[Command(@"rank (?!\d+$)(.*)")]
	public static void RankByUser([Group(1)] string desiredUser, string user, bool isWhisper) { Leaderboard.Instance.GetRank(desiredUser, out var entry); ShowRank(entry, desiredUser, user, isWhisper); }

	/// <name>Get Previous Log</name>
	/// <syntax>log</syntax>
	/// <summary>前回の爆弾のログを表示する。</summary>
	[Command(@"(log|analysis)")]
	public static void Log() => LogUploader.PostToChat(LogUploader.Instance.previousUrl, "前回の爆弾の解析ログ: {0}");

	/// <name>Get Log</name>
	/// <syntax>lognow</syntax>
	/// <summary>現在のログを表示する。</summary>
	/// <restriction>Admin</restriction>
	[Command("(log|analysis)now", AccessLevel.Admin, AccessLevel.Admin)]
	public static void LogNow(string user, bool isWhisper) => LogUploader.Instance.GetAnalyzerUrl(url => IRCConnection.SendMessage(url, user, !isWhisper));

	/// <name>Toggle Short URLs</name>
	/// <syntax>shorturl</syntax>
	/// <summary>Toggles shortened URLs.</summary>
	[Command(@"shorturl")]
	public static void ShortURL(string user, bool isWhisper) => IRCConnection.SendMessage(string.Format((UrlHelper.ToggleMode()) ? "URL短縮を有効にしました。" : "URL短縮を無効にしました。"), user, !isWhisper);

	/// <name>Build Date</name>
	/// <syntax>builddate</syntax>
	/// <summary>TPをビルドした日を表示する。</summary>
	[Command(@"(?:builddate|version)")]
	public static void BuildDate(string user, bool isWhisper)
	{
		DateTime date = Updater.GetCurrentBuildDateTime();
		IRCConnection.SendMessage($"このTPのビルド日：{date:yyyy-MM-dd HH:mm:ss}", user, !isWhisper);
	}

	/// <name>Read Setting</name>
	/// <syntax>readsetting [setting]</syntax>
	/// <summary>設定を読み込む。</summary>
	/// <restriction>Mod</restriction>
	[Command(@"(?:read|write|change|set) *settings? +(\S+)", AccessLevel.Mod, AccessLevel.Mod)]
	public static void ReadSetting([Group(1)] string settingName, string user, bool isWhisper) => IRCConnection.SendMessage(TwitchPlaySettings.GetSetting(settingName), user, !isWhisper);

	/// <name>Write Setting</name>
	/// <syntax>writesetting [setting] [value]</syntax>
	/// <summary>設定を特定の値に設定する。</summary>
	/// <restriction>SuperUser</restriction>
	[Command(@"(?:write|change|set) *settings? +(\S+) +(.+)", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static void WriteSetting([Group(1)] string settingName, [Group(2)] string newValue, string user, bool isWhisper)
	{
		var result = TwitchPlaySettings.ChangeSetting(settingName, newValue);
		IRCConnection.SendMessage(result.Second, user, !isWhisper);
		if (result.First)
			TwitchPlaySettings.WriteDataToFile();
	}

	/// <name>Read Module Information</name>
	/// <syntax>readmodule [information] [module]</syntax>
	/// <summary>モジュールの情報を呼び出す。</summary>
	[Command(@"read *module *(help(?: *message)?|manual(?: *code)?|score|points|compatibility(?: *mode)?|statuslight|(?:camera *|module *)?pin *allowed|strike(?: *penalty)|colou?r|(?:valid *)?commands|unclaimable|announce(?:ment| *module)?) +(.+)")]
	public static void ReadModuleInformation([Group(1)] string command, [Group(2)] string parameter, string user, bool isWhisper)
	{
		var modules = ComponentSolverFactory.GetModuleInformation().Where(x => x.moduleDisplayName.ContainsIgnoreCase(parameter)).ToList();
		switch (modules.Count)
		{
			case 0:
				modules = ComponentSolverFactory.GetModuleInformation().Where(x => x.moduleID.ContainsIgnoreCase(parameter)).ToList();
				if (modules.Count == 1) goto case 1;
				if (modules.Count > 1)
				{
					var onemoduleID = modules.Where(x => x.moduleID.EqualsIgnoreCase(parameter)).ToList();
					if (onemoduleID.Count == 1)
					{
						modules = onemoduleID;
						goto case 1;
					}
					goto default;
				}

				IRCConnection.SendMessage($@"{parameter}というモジュールは見つかりませんでした。", user, !isWhisper);
				break;

			case 1:
				var moduleName = $"“{modules[0].moduleDisplayName}” ({modules[0].moduleID})";
				switch (command.ToLowerInvariant())
				{
					case "help":
					case "helpmessage":
					case "help message":
						IRCConnection.SendMessage($"モジュール{moduleName}のヘルプメッセージ：{modules[0].helpText}", user, !isWhisper);
						break;
					case "manual":
					case "manualcode":
					case "manual code":
						IRCConnection.SendMessage($"モジュール{moduleName}のマニュアルページ：{(string.IsNullOrEmpty(modules[0].manualCode) ? modules[0].moduleDisplayName : modules[0].manualCode)}", user, !isWhisper);
						break;
					case "points":
					case "score":
						IRCConnection.SendMessage($"モジュール{moduleName}のスコア：{modules[0].ScoreExplanation}", user, !isWhisper);
						break;
					case "statuslight":
						IRCConnection.SendMessage($"モジュール{moduleName}のステータスライトの位置：{modules[0].statusLightPosition}", user, !isWhisper);
						break;
					case "module pin allowed":
					case "camera pin allowed":
					case "module pinallowed":
					case "camera pinallowed":
					case "modulepin allowed":
					case "camerapin allowed":
					case "modulepinallowed":
					case "camerapinallowed":
					case "pinallowed":
					case "pin allowed":
						IRCConnection.SendMessage($"モジュール{moduleName}のカメラ固定: {(modules[0].CameraPinningAlwaysAllowed ? "常に許可" : "通常禁止")}", user, !isWhisper);
						break;
					case "color":
					case "colour":
						var moduleColor = JsonConvert.SerializeObject(TwitchPlaySettings.data.UnclaimedColor, Formatting.None, new ColorConverter());
						if (modules[0].unclaimedColor != new Color())
							moduleColor = JsonConvert.SerializeObject(modules[0].unclaimedColor, Formatting.None, new ColorConverter());
						IRCConnection.SendMessage($"モジュール{moduleName}の非割り当て状態のときの色：{moduleColor}", user, !isWhisper);
						break;
					case "commands":
					case "valid commands":
					case "validcommands":
						IRCConnection.SendMessage($"モジュール{moduleName}の有効なコマンド：{modules[0].validCommands}", user, !isWhisper);
						break;
					case "announcemodule":
					case "announce module":
					case "announce":
					case "announcement":
						IRCConnection.SendMessage($"モジュール{moduleName}の爆弾開始時のアナウンス：{(modules[0].announceModule ? "する" : "しない")}", user, !isWhisper);
						break;
					case "unclaimable":
						IRCConnection.SendMessage($"モジュール{moduleName}の割り当て禁止：{(modules[0].unclaimable ? "禁止" : "許可")}", user, !isWhisper);
						break;
					case "compatibility":
					case "compatibility mode":
					case "compatibilitymode":
						IRCConnection.SendMessage($"モジュール{moduleName}の互換性モード：{(modules[0].CompatibilityMode ? "有効" : "無効")}", user, !isWhisper);
						break;
				}
				break;

			default:
				var oneModule = modules.Where(x => x.moduleDisplayName.EqualsIgnoreCase(parameter)).ToList();
				if (oneModule.Count == 1)
				{
					modules = oneModule;
					goto case 1;
				}

				IRCConnection.SendMessage($"検索文字列に一致するモジュールが複数見つかりました：{modules.Take(5).Select(x => $"“{x.moduleDisplayName}” ({x.moduleID})").Join(", ")}", user, !isWhisper);
				break;
		}
	}

	/// <name>Write Module Information</name>
	/// <syntax>writemodule [information] [module] [value]</syntax>
	/// <summary>モジュールの情報を特定の値にする。</summary>
	/// <restriction>Admin</restriction>
	[Command(@"(?:write|change|set) *module *(help(?: *message)?|manual(?: *code)?|score|points|compatibility(?: *mode)?|statuslight|(?:camera *|module *)?pin *allowed|strike(?: *penalty)|colou?r|unclaimable|announce(?:ment| *module)?) +(.+);(.*)", AccessLevel.Admin, AccessLevel.Admin)]
	public static void WriteModuleInformation([Group(1)] string command, [Group(2)] string search, [Group(3)] string changeTo, string user, bool isWhisper)
	{
		var modules = ComponentSolverFactory.GetModuleInformation().Where(x => x.moduleDisplayName.ContainsIgnoreCase(search)).ToList();
		switch (modules.Count)
		{
			case 0:
				modules = ComponentSolverFactory.GetModuleInformation().Where(x => x.moduleID.ContainsIgnoreCase(search)).ToList();
				if (modules.Count == 1)
					goto case 1;
				if (modules.Count > 1)
				{
					var onemoduleID = modules.Where(x => x.moduleID.Equals(search, StringComparison.InvariantCultureIgnoreCase)).ToList();
					if (onemoduleID.Count == 1)
					{
						modules = onemoduleID;
						goto case 1;
					}
					goto default;
				}

				IRCConnection.SendMessage($"{search}というモジュールは見つかりませんでした。", user, !isWhisper);
				break;

			case 1:
				var module = modules[0];
				var moduleName = $"“{module.moduleDisplayName}” ({module.moduleID})";
				var defaultModule = ComponentSolverFactory.GetDefaultInformation(module.moduleID);
				switch (command.ToLowerInvariant())
				{
					case "help":
					case "helpmessage":
					case "help message":
						if (string.IsNullOrEmpty(changeTo))
						{
							module.helpTextOverride = false;
							module.helpText = defaultModule.helpText;
						}
						else
						{
							module.helpText = changeTo;
							module.helpTextOverride = true;
						}
						IRCConnection.SendMessage($"モジュール{moduleName}のヘルプメッセージは次に書き換わりました：{module.helpText}", user, !isWhisper);
						break;
					case "manual":
					case "manualcode":
					case "manual code":
						if (string.IsNullOrEmpty(changeTo))
						{
							module.manualCodeOverride = false;
							module.manualCode = defaultModule.manualCode;
						}
						else
						{
							module.manualCode = changeTo;
							module.manualCodeOverride = true;
						}

						IRCConnection.SendMessage($"モジュール{moduleName}のマニュアルは次に書き換わりました：{(string.IsNullOrEmpty(module.manualCode) ? module.moduleDisplayName : module.manualCode)}", user, !isWhisper);
						break;
					case "points":
					case "score":
						var fileModule = Array.Find(ModuleData.LastRead, info => info.moduleID == module.moduleID);
						if (fileModule != null)
						{
							fileModule.scoreString = changeTo;
							module.scoreString = changeTo;
							module.scoreStringOverride = true;
							IRCConnection.SendMessage($"モジュール{moduleName}のスコアは次に書き換わりました：{module.scoreString}", user, !isWhisper);
						}
						break;
					case "statuslight":
						switch (changeTo.ToLowerInvariant())
						{
							case "bl":
							case "bottomleft":
							case "bottom left":
								module.statusLightPosition = StatusLightPosition.BottomLeft;
								break;
							case "br":
							case "bottomright":
							case "bottom right":
								module.statusLightPosition = StatusLightPosition.BottomRight;
								break;
							case "tr":
							case "topright":
							case "top right":
								module.statusLightPosition = StatusLightPosition.TopRight;
								break;
							case "tl":
							case "topleft":
							case "top left":
								module.statusLightPosition = StatusLightPosition.TopLeft;
								break;
							case "c":
							case "center":
								module.statusLightPosition = StatusLightPosition.Center;
								break;
							default:
								module.statusLightPosition = StatusLightPosition.Default;
								break;
						}
						IRCConnection.SendMessage($"モジュール{moduleName}のステータスライトの位置は次に書き換わりました：{module.statusLightPosition}", user, !isWhisper);
						break;
					case "module pin allowed":
					case "camera pin allowed":
					case "module pinallowed":
					case "camera pinallowed":
					case "modulepin allowed":
					case "camerapin allowed":
					case "modulepinallowed":
					case "camerapinallowed":
					case "pinallowed":
					case "pin allowed":
						module.CameraPinningAlwaysAllowed = changeTo.ContainsIgnoreCase("true") || changeTo.ContainsIgnoreCase("yes");
						IRCConnection.SendMessage($"モジュール{moduleName}のカメラ固定が次に変更されました: {(modules[0].CameraPinningAlwaysAllowed ? "常に許可" : "通常禁止")}", user, !isWhisper);
						break;
					case "color":
					case "colour":
						string moduleColor;
						try
						{
							var newModuleColor = SettingsConverter.Deserialize<Color>(changeTo);
							moduleColor = newModuleColor == new Color()
								? JsonConvert.SerializeObject(modules[0].unclaimedColor, Formatting.None, new ColorConverter())
								: changeTo;
							module.unclaimedColor = newModuleColor == new Color()
								? defaultModule.unclaimedColor
								: newModuleColor;
						}
						catch
						{
							moduleColor = JsonConvert.SerializeObject(TwitchPlaySettings.data.UnclaimedColor, Formatting.None, new ColorConverter());
							if (defaultModule.unclaimedColor != new Color())
								moduleColor = JsonConvert.SerializeObject(modules[0].unclaimedColor, Formatting.None, new ColorConverter());
							module.unclaimedColor = defaultModule.unclaimedColor;
						}

						IRCConnection.SendMessage($"モジュール{moduleName}の非割り当て時の色が次に変化しました: {moduleColor}", user, !isWhisper);
						break;
					case "announcemodule":
					case "announce module":
					case "announce":
					case "announcement":
						module.announceModule = changeTo.ContainsIgnoreCase("true") || changeTo.ContainsIgnoreCase("yes");
						IRCConnection.SendMessage($"モジュール{moduleName}の爆弾開始時のアナウンスを次に変更しました：{(modules[0].announceModule ? "する" : "しない")}", user, !isWhisper);
						break;
					case "unclaimable":
						module.unclaimable = changeTo.ContainsIgnoreCase("true") || changeTo.ContainsIgnoreCase("yes");
						IRCConnection.SendMessage($"モジュール{moduleName}の割り当て禁止は次に書き換わりました：{(modules[0].unclaimable ? "禁止" : "許可")}", user, !isWhisper);
						break;
					case "compatibility":
					case "compatibility mode":
					case "compatibilitymode":
						if (module.builtIntoTwitchPlays)
						{
							IRCConnection.SendMessage($"モジュール「{moduleName}」はTP本体に内蔵されているため、互換性モードを変更できません。このモジュールに問題がある場合、https://github.com/samfundev/KtaneTwitchPlays/issues までお知らせください。", user, !isWhisper);
							break;
						}
						module.CompatibilityMode = changeTo.ContainsIgnoreCase("true") || changeTo.ContainsIgnoreCase("yes") || changeTo.ContainsIgnoreCase("enable");
						IRCConnection.SendMessage($"モジュール「{moduleName}」の互換性モードが次に変更されました：{(modules[0].CompatibilityMode ? "有効" : "無効")}", user, !isWhisper);
						break;
				}
				ModuleData.DataHasChanged = true;
				ModuleData.WriteDataToFile();

				break;
			default:
				var onemodule = modules.Where(x => x.moduleDisplayName.Equals(search)).ToList();
				if (onemodule.Count == 1)
				{
					modules = onemodule;
					goto case 1;
				}

				IRCConnection.SendMessage($"検索文字列に一致するモジュールが複数見つかりました：{modules.Take(5).Select(x => $"“{x.moduleDisplayName}” ({x.moduleID})").Join(", ")}", user, !isWhisper);
				break;
		}
	}

	/// <name>Reset Setting</name>
	/// <syntax>resetsetting [setting]</syntax>
	/// <summary>設定をデフォルトにする。</summary>
	/// <restriction>SuperUser</restriction>
	[Command(@"(?:erase|remove|reset) ?settings? (\S+)", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static void ResetSetting([Group(1)] string parameter, string user, bool isWhisper)
	{
		var result = TwitchPlaySettings.ResetSettingToDefault(parameter);
		IRCConnection.SendMessage($"{result.Second}", user, !isWhisper);
		if (result.First)
			TwitchPlaySettings.WriteDataToFile();
	}

	/// <name>Timeout User with Reason</name>
	/// <syntax>timeout [user] [length] [reason]</syntax>
	/// <summary>理由を含め、TPから一時的にユーザーをBANする。</summary>
	/// <argument name="length">How long the user should be banned for in seconds.</argument>
	/// <restriction>Mod</restriction>
	[Command(@"timeout +(\S+) +(\d+) +(.+)")]
	public static void BanUser([Group(1)] string userToBan, [Group(2)] int banTimeout, [Group(3)] string reason, string user, bool isWhisper) => UserAccess.TimeoutUser(userToBan, user, reason, banTimeout, isWhisper);

	/// <name>Timeout User</name>
	/// <syntax>timeout [user] [length]</syntax>
	/// <summary>TPから一時的にユーザーをBANする。</summary>
	/// <argument name="length">How long the user should be banned for in seconds.</argument>
	/// <restriction>Mod</restriction>
	[Command(@"timeout +(\S+) +(\d+)")]
	public static void BanUserForNoReason([Group(1)] string userToBan, [Group(2)] int banTimeout, string user, bool isWhisper) => UserAccess.TimeoutUser(userToBan, user, null, banTimeout, isWhisper);

	/// <name>Ban User with Reason</name>
	/// <syntax>ban [user] [reason]</syntax>
	/// <summary>理由を含め、TPから永続的にユーザーをBANする。</summary>
	/// <restriction>Mod</restriction>
	[Command(@"ban +(\S+) +(.+)")]
	public static void BanUser([Group(1)] string userToBan, [Group(2)] string reason, string user, bool isWhisper) => UserAccess.BanUser(userToBan, user, reason, isWhisper);

	/// <name>Ban User</name>
	/// <syntax>ban [user]</syntax>
	/// <summary>TPから永続的にユーザーをBANする。</summary>
	/// <restriction>Mod</restriction>
	[Command(@"ban +(\S+)")]
	public static void BanUserForNoReason([Group(1)] string userToBan, string user, bool isWhisper) => UserAccess.BanUser(userToBan, user, null, isWhisper);

	/// <name>Unban User</name>
	/// <syntax>unban [user]</syntax>
	/// <summary>ユーザーのBANを解除する。</summary>
	/// <restriction>Mod</restriction>
	[Command(@"unban +(\S+)")]
	public static void UnbanUser([Group(1)] string userToUnban, string user, bool isWhisper) => UserAccess.UnbanUser(userToUnban, user, isWhisper);

	/// <name>Is Banned</name>
	/// <syntax>isbanned [users]</syntax>
	/// <summary>ユーザーがBANされているか確認する。</summary>
	/// <restriction>Mod</restriction>
	[Command(@"(isbanned|banstats|bandata) +(\S+)", AccessLevel.Mod, AccessLevel.Mod)]
	public static void IsBanned([Group(1)] string usersToCheck, string user, bool isWhisper)
	{
		bool found = false;
		var bandata = UserAccess.GetBans();
		foreach (string person in usersToCheck.Split(';'))
		{
			string adjperson = person.Trim();
			if (bandata.Keys.Contains(adjperson))
			{
				bandata.TryGetValue(adjperson, out var value);
				if (double.IsPositiveInfinity(value.BanExpiry))
					IRCConnection.SendMessage($"ユーザー：{adjperson}、BAN実施者：{value.BannedBy}{(string.IsNullOrEmpty(value.BannedReason) ? $"理由：{value.BannedReason}." : ".")}。このBANは永続的です。", user, !isWhisper);
				else
					IRCConnection.SendMessage($"ユーザー：{adjperson}、BAN実施者：{value.BannedBy}{(string.IsNullOrEmpty(value.BannedReason) ? $"理由：{value.BannedReason}." : ".")}。あと{value.BanExpiry - DateTime.Now.TotalSeconds()}秒でBANは解除されます。", user, !isWhisper);
				found = true;
			}
		}
		if (!found)
			IRCConnection.SendMessage("このユーザーのBAN情報はありません。", user, !isWhisper);
	}

	/// <name>Add red Player</name>
	/// <syntax>addred [user]</syntax>
	/// <summary>VSモードで赤組に入れる</summary>
	/// <restriction>Mod</restriction>
	[Command(@"addred (.+)", AccessLevel.Mod, AccessLevel.Mod)]
	public static void AddRed([Group(1)] string targetUser)
	{
		targetUser = targetUser.FormatUsername();
		Leaderboard.Instance.MakeRed(targetUser);
		IRCConnection.SendMessage($"@{targetUser}は赤組になりました。");
	}

	/// <name>Add White Player</name>
	/// <syntax>addwhite [user]</syntax>
	/// <summary>VSモードで白組に入れる</summary>
	/// <restriction>Mod</restriction>
	[Command(@"addwhite (.+)", AccessLevel.Mod, AccessLevel.Mod)]
	public static void AddWhite([Group(1)] string targetUser)
	{
		targetUser = targetUser.FormatUsername();
		Leaderboard.Instance.MakeWhite(targetUser);
		IRCConnection.SendMessage($"@{targetUser}は白組になりました。");
	}

	/// <name>Join Versus</name>
	/// <syntax>join</syntax>
	/// <summary>いずれかのチームに入る</summary>
	[Command(@"join")]
	public static void JoinAnyTeam(string user)
	{
		bool _inGame = TwitchGame.Instance.VSSetFlag;

		if (!OtherModes.VSModeOn)
		{
			IRCConnection.SendMessage($"@{user}さん：VSモードは有効ではありません。");
			return;
		}
		if (!TwitchPlaySettings.data.AutoSetVSModeTeams)
		{
			IRCConnection.SendMessage($"@{user}さん：チームは手動で割り当てられます。チームを指定してください。");
			return;
		}
		OtherModes.Team? team = Leaderboard.Instance.GetTeam(user);
		if (team != null)
		{
			IRCConnection.SendMessage($@"{user}さん：すでに{team.ToString().ToLower()}組に参加しています。");
			return;
		}
		if (_inGame && !TwitchPlaySettings.data.VSModePlayerLockout)
		{
			AddVSPlayer(user);
			return;
		}

		Leaderboard.Instance.GetEntry(user).Team = OtherModes.Team.Undecided;
		IRCConnection.SendMessage($"@{user}さん：{(_inGame ? "爆弾がすでに起動しています。" : "")}次のVSモードから参加となります。");
	}

	/// <name>Clear Versus Players</name>
	/// <syntax>clearvsplayers</syntax>
	/// <summary>VSモードの人を削除する。</summary>
	/// <restriction>Admin</restriction>
	[Command(@"clearvsplayers", AccessLevel.Admin, AccessLevel.Admin)]
	public static void ClearVSPlayers()
	{
		foreach (var entry in Leaderboard.Instance.GetVSEntries())
			entry.Team = null;
		IRCConnection.SendMessage("VSモードのプレイヤー情報が消去されました。");
	}

	/// <name>Versus Players</name>
	/// <syntax>players</syntax>
	/// <summary>各チームのプレイヤー情報を送る。</summary>
	[Command(@"players")]
	public static void ReadTeams()
	{
		if (!TwitchPlaySettings.data.AutoSetVSModeTeams)
		{
			IRCConnection.SendMessage("現在のモードではこのコマンドを利用できません。");
			return;
		}

		var byTeam = Leaderboard.Instance.GetVSEntries().ToDictionary(entry => entry.Team, entry => entry.UserName);
		foreach (var pair in byTeam)
		{
			IRCConnection.SendMessage($"${pair.Value.Length}名の{pair.Key}組プレイヤーが参加しています：@{pair.Value.Join(", @")}");
		}
	}

	/// <name>Join Team</name>
	/// <syntax>join [team]</syntax>
	/// <summary>組を指定して参加する。</summary>
	[Command(@"join (red|white)")]
	public static void JoinWantedTeam([Group(1)] string team, string user, bool isWhisper)
	{
		OtherModes.Team target;
		switch(team) {
			case "白組":
			case "white":
				target = OtherModes.Team.White;
				break;
			default:
				target = OtherModes.Team.Red;
				break;
		}
		Leaderboard.Instance.GetRank(user, out Leaderboard.LeaderboardEntry entry);
		if (TwitchPlaySettings.data.AutoSetVSModeTeams)
		{
			IRCConnection.SendMessage($"@{user}さん：チームは自動的に割り当てられます。!joinコマンドで参加してください。");
			return;
		}
		// ReSharper disable once SwitchStatementMissingSomeCases
		switch (target)
		{
			case OtherModes.Team.Red:
				if (entry != null && entry.Team == OtherModes.Team.Red)
				{
					IRCConnection.SendMessage($"@{user}さん：すでに赤組に参加予定です。", user, !isWhisper);
					return;
				}

				if (!Leaderboard.Instance.IsTeamBalanced(OtherModes.Team.Red))
				{
					IRCConnection.SendMessage(
						$"@{user}さん：赤組の人数が多すぎるので赤組に参加できません。後でもう一度試してみ{(entry.Team != OtherModes.Team.White ? "るか、白組に参加し" : "")}てください。",
						user, !isWhisper);
					return;
				}
				Leaderboard.Instance.MakeRed(user);
				IRCConnection.SendMessage($"@{user}さんが赤組に参加しました。", user, !isWhisper);
				break;
			case OtherModes.Team.White:
				if (entry != null && entry.Team == OtherModes.Team.White)
				{
					IRCConnection.SendMessage($"@{user}さん：すでに白組に参加予定です。", user, !isWhisper);
					return;
				}

				if (!Leaderboard.Instance.IsTeamBalanced(OtherModes.Team.White))
				{
					IRCConnection.SendMessage(
						$"@{user}さん：白組の人数が多すぎるので白組に参加できません。後でもう一度試してみ{(entry.Team != OtherModes.Team.White ? "るか、赤組に参加し" : "")}てください。",
						user, !isWhisper);
					return;
				}
				Leaderboard.Instance.MakeWhite(user);
				IRCConnection.SendMessage($"@{user}さんが白組に参加しました。", user, !isWhisper);
				break;
		}
	}

	/// <name>Add/Remove Rank</name>
	/// <syntax>add [username] [rank]\nremove [username] [rank]</syntax>
	/// <summary>ユーザーに権限を追加または削除する。[rank]にはスペースで区切られた複数のランクを指定できる。</summary>
	/// <restriction>Mod</restriction>
	[Command(@"(add|remove) +(\S+) +(.+)", AccessLevel.Mod, AccessLevel.Mod)]
	public static void AddRemoveRole([Group(1)] string command, [Group(2)] string targetUser, [Group(3)] string roles, string user, bool isWhisper)
	{
		targetUser = targetUser.FormatUsername();
		var stepdown = command.Equals("remove", StringComparison.InvariantCultureIgnoreCase) && targetUser.Equals(user, StringComparison.InvariantCultureIgnoreCase);
		if (!stepdown && !UserAccess.HasAccess(user, AccessLevel.Mod, true))
			return;

		var level = AccessLevel.User;
		foreach (string lvl in roles.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
		{
			switch (lvl)
			{
				case "mod":
				case "moderator":
					level |= (stepdown || UserAccess.HasAccess(user, AccessLevel.SuperUser, true)) ? AccessLevel.Mod : AccessLevel.User;
					break;
				case "admin":
				case "administrator":
					level |= (stepdown || UserAccess.HasAccess(user, AccessLevel.SuperUser, true)) ? AccessLevel.Admin : AccessLevel.User;
					break;
				case "superadmin":
				case "superuser":
				case "super-user":
				case "super-admin":
				case "super-mod":
				case "supermod":
					level |= (stepdown || UserAccess.HasAccess(user, AccessLevel.SuperUser, true)) ? AccessLevel.SuperUser : AccessLevel.User;
					break;

				case "defuser":
					level |= AccessLevel.Defuser;
					break;

				case "no-points":
				case "no-score":
				case "noscore":
				case "nopoints":
					level |= UserAccess.HasAccess(user, AccessLevel.Mod, true) ? AccessLevel.NoPoints : AccessLevel.User;
					break;
			}
		}

		if (level == AccessLevel.User)
			return;

		if (command.EqualsIgnoreCase("add"))
		{
			UserAccess.AddUser(targetUser, level);
			IRCConnection.SendMessage(string.Format(TwitchPlaySettings.data.AddedUserPower, level, targetUser), user, !isWhisper);
		}
		else
		{
			UserAccess.RemoveUser(targetUser, level);
			IRCConnection.SendMessage(string.Format(TwitchPlaySettings.data.RemoveUserPower, level, targetUser), user, !isWhisper);
		}
		UserAccess.WriteAccessList();
	}

	/// <name>Moderators</name>
	/// <syntax>moderators</syntax>
	/// <summary>「Mod」権限以上のユーザーを表示する。</summary>
	[Command(@"(tpmods|moderators)")]
	public static void Moderators(string user, bool isWhisper)
	{
		if (!TwitchPlaySettings.data.EnableModeratorsCommand)
		{
			IRCConnection.SendMessage("モデレーターコマンドは無効です。", user, !isWhisper);
			return;
		}
		KeyValuePair<string, AccessLevel>[] moderators = UserAccess.GetUsers().Where(x => !string.IsNullOrEmpty(x.Key) && x.Key != "_usernickname1" && x.Key != "_usernickname2" && x.Key != (TwitchPlaySettings.data.TwitchPlaysDebugUsername.StartsWith("_") ? TwitchPlaySettings.data.TwitchPlaysDebugUsername.ToLowerInvariant() : "_" + TwitchPlaySettings.data.TwitchPlaysDebugUsername.ToLowerInvariant())).ToArray();
		string finalMessage = "現在のモデレーター：";

		string[] streamers = moderators.Where(x => UserAccess.HighestAccessLevel(x.Key) == AccessLevel.Streamer).OrderBy(x => x.Key).Select(x => x.Key).ToArray();
		string[] superusers = moderators.Where(x => UserAccess.HighestAccessLevel(x.Key) == AccessLevel.SuperUser).OrderBy(x => x.Key).Select(x => x.Key).ToArray();
		string[] administrators = moderators.Where(x => UserAccess.HighestAccessLevel(x.Key) == AccessLevel.Admin).OrderBy(x => x.Key).Select(x => x.Key).ToArray();
		string[] mods = moderators.Where(x => UserAccess.HighestAccessLevel(x.Key) == AccessLevel.Mod).OrderBy(x => x.Key).Select(x => x.Key).ToArray();

		if (streamers.Length > 0)
			finalMessage += $"配信者：{streamers.Join(", ")}{(superusers.Length > 0 || administrators.Length > 0 || mods.Length > 0 ? " - " : "")}";
		if (superusers.Length > 0)
			finalMessage += $"スーパーユーザー：{superusers.Join(", ")}{(administrators.Length > 0 || mods.Length > 0 ? " - " : "")}";
		if (administrators.Length > 0)
			finalMessage += $"管理者：{administrators.Join(", ")}{(mods.Length > 0 ? " - " : "")}";
		if (mods.Length > 0)
			finalMessage += $"モデレーター：{mods.Join(", ")}";

		IRCConnection.SendMessage(finalMessage, user, !isWhisper);
	}

	/// <name>Get Access</name>
	/// <syntax>getaccess [users]</syntax>
	/// <summary>特定のユーザーの権限情報を見る。</summary>
	/// <restriction>Mod</restriction>
	[Command(@"(getaccess|accessstats|accessdata) +(.+)", AccessLevel.Mod, AccessLevel.Mod)]
	public static void GetAccess([Group(2)] string targetUsers, string user, bool isWhisper)
	{
		foreach (string person in targetUsers.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
			IRCConnection.SendMessage(string.Format("ユーザー{0}のアクセスレベル：{1}", person, UserAccess.LevelToString(UserAccess.HighestAccessLevel(person))), user, !isWhisper);
	}

	/// <name>Run Help</name>
	/// <syntax>run</syntax>
	/// <summary>runコマンドの使い方を送る。</summary>
	[Command(@"run")]
	public static void RunHelp()
	{
		string[] validDistributions = TwitchPlaySettings.data.ModDistributionSettings.Where(x => x.Value.Enabled && !x.Value.Hidden).Select(x => x.Key).ToArray();
		IRCConnection.SendMessage(validDistributions.Length > 0
			? $"使用方法: !run <モジュール数> <分配> | 有効な分配: {validDistributions.Join(", ")}"
			: "!runは無効化されています。");
	}

	/// <name>Run VS</name>
	/// <syntax>run [modules] [distribution] [Redhp] [Whitehp]</syntax>
	/// <summary>VSモードで実行する。</summary>
	[Command(@"run +(\d+) +(.*) +(\d+) +(\d+)")]
	public static IEnumerator RunVSHP(string user, bool isWhisper, [Group(1)] int modules,
	[Group(2)] string distributionName, [Group(3)] int RedHP, [Group(4)] int WhiteHP, KMGameInfo inf) => RunWrapper(
	user, isWhisper,
	() =>
	{
		if (!TwitchPlaySettings.data.ModDistributionSettings.TryGetValue(distributionName, out var distribution))
		{
			IRCConnection.SendMessage($"{distributionName}という分配は存在しません。");
			return null;
		}
		if (TwitchPlaySettings.data.AutoSetVSModeTeams)
		{
			string[] allPlayers = Leaderboard.Instance.GetVSEntries().Select(entry => entry.UserName).OrderBy(Leaderboard.Instance.GetTrueRank).ToArray();
			if (allPlayers.Length < 2)
			{
				IRCConnection.SendMessage("VSモードに十分な人数が揃っていません。");
				return null;
			}

			if (TwitchPlaySettings.data.VSModeBalancedTeams)
			{
				for (int i = 0; i < allPlayers.Length; i++) AddVSPlayer(allPlayers[i]);
			}
			else
			{
				int RedCount = allPlayers.Length < 4 ? 1 : allPlayers.Length * TwitchPlaySettings.data.VSModeRedSplit / 100;

				for (int i = 0; i < RedCount; i++) AddRed(allPlayers[i]);
				for (int i = RedCount; i < allPlayers.Length; i++) AddWhite(allPlayers[i]);
			}
			TwitchGame.Instance.VSSetFlag = true;
		}
		else
		{
			if (!Leaderboard.Instance.IsAnyWhite())
			{
				IRCConnection.SendMessage("白組に割り当てられた人がいないため、VSモードを開始できません。");
				return null;
			}

			if (!Leaderboard.Instance.IsAnyRed())
			{
				IRCConnection.SendMessage("赤組に割り当てられた人がいないため、VSモードを開始できません。");
				return null;
			}
		}

		OtherModes.RedHealth = RedHP;
		OtherModes.WhiteHealth = WhiteHP;

		return RunDistribution(user, modules, inf, distribution);
	}, true);

	/// <name>Assign Any</name>
	/// <syntax>assignany [user]</syntax>
	/// <summary>特定ユーザーをVSモードのチームに割り当てる。割り当ては均等になるように行われる。</summary>
	/// <restriction>Mod</restriction>
	[Command(@"assignany (.+)", AccessLevel.Mod, AccessLevel.Mod)]
	public static void AddVSPlayer([Group(1)] string targetUser)
	{
		int diff = Leaderboard.Instance.GetVSEntries().Sum(entry => (int) entry.Team);
		if (diff > 1)
		{
			AddWhite(targetUser);
		}
		else if (diff < -1)
		{
			AddRed(targetUser);
		}
		else
		{
			int rand = Random.Range(0, 2);
			if (rand == 0)
			{
				AddWhite(targetUser);
			}
			else
			{
				AddRed(targetUser);
			}
		}
	}

	/// <name>Run Specific</name>
	/// <syntax>run [distribution] [modules]</syntax>
	/// <summary>設定されたモジュール数と分配で実行する。[distribution]には、vanilla, light, mixed, heavy, modsなどがある。。mixedlightやextralightのような組み合わせも可能。バニラすべてからMODモジュールすべてまで選べる。</summary>
	[Command(@"run +(.*) +(\d+)")]
	public static IEnumerator RunSpecific(string user, bool isWhisper, [Group(1)] string distributionName, [Group(2)] int modules, KMGameInfo inf) => RunSpecific(user, isWhisper, modules, distributionName, inf);
	[Command(@"run +(\d+) +(.*)")]
	public static IEnumerator RunSpecific(string user, bool isWhisper, [Group(1)] int modules, [Group(2)] string distributionName, KMGameInfo inf) => RunWrapper(user, isWhisper, () =>
	{
		if (!TwitchPlaySettings.data.ModDistributionSettings.TryGetValue(distributionName, out var distribution))
		{
			IRCConnection.SendMessage($"{distributionName}という分配は存在しません。");
			return null;
		}

		if (OtherModes.VSModeOn)
		{
			IRCConnection.SendMessage("このrunコマンドの形式はVSモードでは実行できません。");
			return null;
		}
		return RunDistribution(user, modules, inf, distribution);
	});

	/// <name>Run Mission</name>
	/// <syntax>run [mission name]</syntax>
	/// <summary>特定名のミッションを実行する。「Mod」権限以上の場合、ミッションIDでも実行できる。</summary>
	[Command(@"run +(?!.* +\d+$|\d+ +.*$)(.+)")]
	public static IEnumerator RunMission(string user, bool isWhisper, [Group(1)] string textAfter, KMGameInfo inf) => RunWrapper(user, isWhisper, () =>
	{
		if (OtherModes.VSModeOn)
		{
			IRCConnection.SendMessage("VSモード中はミッションを実行できません。");
			return null;
		}

		string missionID = null;
		string failureMessage = null;
		if (UserAccess.HasAccess(user, AccessLevel.Mod, true))
			missionID = ResolveMissionID(inf, textAfter, out failureMessage);

		if (missionID == null && TwitchPlaySettings.data.CustomMissions.ContainsKey(textAfter))
			missionID = ResolveMissionID(inf, TwitchPlaySettings.data.CustomMissions[textAfter], out failureMessage);

		if (missionID == null)
		{
			IRCConnection.SendMessage(failureMessage);
			return null;
		}

		return RunMissionCoroutine(missionID);
	});

	/// <name>Run Raw</name>
	/// <syntax>runraw [mission id]</syntax>
	/// <summary>ミッションを完全なIDで実行する。例: mod_TwitchPlays_tpFMNHell,firsttime。必要なモジュールがミッションに含まれているか、IDが正しくない場合、ソフトロックされる。</summary>
	/// <restriction>Admin</restriction>
	[Command(@"runraw +(.+)", AccessLevel.Admin, AccessLevel.Admin)]
	public static IEnumerator RunRaw([Group(1)] string missionName) => RunMissionCoroutine(missionName);

	/// <name>Run Raw Seed</name>
	/// <syntax>runrawseed [seed] [mission id]</syntax>
	/// <summary>runrawコマンドをルールシード付きで実行する。</summary>
	/// <restriction>Admin</restriction>
	[Command(@"runrawseed +(\d+) +(.+)", AccessLevel.Admin, AccessLevel.Admin)]
	public static IEnumerator RunRawSeed([Group(1)] string seed, [Group(2)] string missionName) => RunMissionCoroutine(missionName, seed);

	/// <name>Profile Help</name>
	/// <syntax>profile help</syntax>
	/// <summary>プロファイルコマンドのヘルプを表示する。</summary>
	[Command(@"profiles? help")]
	public static void ProfileHelp(string user, bool isWhisper) =>
		IRCConnection.SendMessage("!profile enable <名前> [プロファイルを有効化する] | !profile disable <名前> [プロファイルを無効化する] | !profile enabled [有効なプロファイル一覧] | !profile list [プロファイル一覧] ", user, !isWhisper);

	/// <name>Profile Enable</name>
	/// <syntax>profile enable [name]</syntax>
	/// <summary>プロファイルを有効にする。</summary>
	[Command(@"profiles? +(?:enable|activate) +(.+)")]
	public static void ProfileEnable([Group(1)] string profileName, string user, bool isWhisper) => ProfileWrapper(profileName, user, isWhisper, (filename, profileString) =>
	{
		IRCConnection.SendMessage(ProfileHelper.Enable(filename) ?
			$"プロファイル「{profileString}」を有効にしました。" :
			string.Format(TwitchPlaySettings.data.ProfileActionUseless, profileString, "enabled"), user, !isWhisper);
	});

	/// <name>Profile Disable</name>
	/// <syntax>profile disable [name]</syntax>
	/// <summary>プロファイルを無効にする。</summary>
	[Command(@"profiles? +(?:disable|deactivate) +(.+)")]
	public static void ProfileDisable([Group(1)] string profileName, string user, bool isWhisper) => ProfileWrapper(profileName, user, isWhisper, (filename, profileString) =>
	{
		IRCConnection.SendMessage(ProfileHelper.Disable(filename) ?
			$"プロファイル「{profileString}」を無効にしました。" :
			string.Format(TwitchPlaySettings.data.ProfileActionUseless, profileString, "disabled"), user, !isWhisper);
	});

	/// <name>Profile Enabled</name>
	/// <syntax>profile enabled</syntax>
	/// <summary>有効なプロファイル一覧を表示する。</summary>
	[Command(@"profiles? +enabled(?:list)?")]
	public static void ProfilesListEnabled(string user, bool isWhisper) => IRCConnection.SendMessage(string.Format(TwitchPlaySettings.data.ProfileListEnabled, ProfileHelper.Profiles.Select(str => str.Replace('_', ' ')).Intersect(TwitchPlaySettings.data.ProfileWhitelist).DefaultIfEmpty("(none)").Join(", ")), user, !isWhisper);

	/// <name>Profile List</name>
	/// <syntax>profile list</syntax>
	/// <summary>利用可能なプロファイル一覧を表示する。</summary>
	[Command(@"profiles? +(?:list|all)?")]
	public static void ProfilesListAll(string user, bool isWhisper) => IRCConnection.SendMessage(string.Format(TwitchPlaySettings.data.ProfileListAll, TwitchPlaySettings.data.ProfileWhitelist.Join(", ")), user, !isWhisper);

	/// <name>Profile Add/Remove Module</name>
	/// <syntax>profile add [module] [profile]\nprofile remove [module] [profile]</syntax>
	/// <summary>プロファイルにモジュールを追加または削除する。[module] には部分的なモジュール名か ID を指定し、名前にスペースがある場合は引用符で囲むことで指定できる。[profile] はプロファイル名の一部でもよい。</summary>
	/// <restriction>Admin</restriction>
	[Command("profiles? +(?:(add)|remove) +(\"?)(.+)\\2 +(.+)", AccessLevel.Admin, AccessLevel.Admin)]
	public static void ProfileModule(string user, bool isWhisper, [Group(1)] bool adding, [Group(3)] string module, [Group(4)] string profileName)
	{
		if (!ComponentSolverFactory.GetModuleInformation().Search(module, modInfo => modInfo.moduleDisplayName, out ModuleInformation moduleInfo, out string message) &&
			!ComponentSolverFactory.GetModuleInformation().Search(module, modInfo => modInfo.moduleID, out moduleInfo, out message))
		{
			IRCConnection.SendMessage(message);
			return;
		}

		if (!Directory.GetFiles(ProfileHelper.ProfileFolder, "*.json").Search(profileName, path => Path.GetFileNameWithoutExtension(path).Replace('_', ' '), out string profilePath, out message))
		{
			IRCConnection.SendMessage(message);
			return;
		}

		var cleanProfileName = Path.GetFileNameWithoutExtension(profilePath).Replace('_', ' ');
		var success = ProfileHelper.SetState(cleanProfileName, moduleInfo.moduleID, !adding);
		IRCConnection.SendMessage(success ?
			$"{moduleInfo.moduleTranslatedName ?? moduleInfo.moduleDisplayName}が{cleanProfileName}{(adding ? "に追加" : "から消去")}されました。" :
			$"{moduleInfo.moduleTranslatedName ?? moduleInfo.moduleDisplayName}はすでに{cleanProfileName}に存在{(adding ? "します" : "していません")}。",
			user, !isWhisper
		);
	}

	/// <name>Profile Create</name>
	/// <syntax>profile create [profile] [module]</syntax>
	/// <summary>無効なモジュールを設定する新しいプロファイルを作成する。[profile] は新しいプロファイル名でなければいけない。[module] には部分的なモジュール名または ID を指定し、名前にスペースがある場合は引用符で囲むことで指定できる。</summary>
	/// <restriction>Admin</restriction>
	[Command("profiles? +create +(\"?)(.+)\\1 +(\"?)(.+)\\3", AccessLevel.Admin, AccessLevel.Admin)]
	public static void ProfileCreate(string user, bool isWhisper, [Group(2)] string profileName, [Group(4)] string module)
	{
		if (!ComponentSolverFactory.GetModuleInformation().Search(module, modInfo => modInfo.moduleDisplayName, out ModuleInformation moduleInfo, out string message) &&
			!ComponentSolverFactory.GetModuleInformation().Search(module, modInfo => modInfo.moduleID, out moduleInfo, out message))
		{
			IRCConnection.SendMessage(message);
			return;
		}

		var fileName = profileName.Replace(' ', '_');
		var profilePath = Path.Combine(ProfileHelper.ProfileFolder, $"{fileName}.json");
		if (File.Exists(profilePath))
		{
			IRCConnection.SendMessage(message);
			return;
		}

		ProfileHelper.Write(fileName, new[] { module });
		TwitchPlaySettings.data.ProfileWhitelist.Add(profileName);
		TwitchPlaySettings.WriteDataToFile();
		IRCConnection.SendMessage($"モジュール「{moduleInfo.moduleDisplayName}」から「{profileName}」を作成しました。",
			user, !isWhisper
		);
	}
	
	// <name>Profile Delete</name>
	/// <syntax>profile delete [profile]</syntax>
	/// <summary>特定の名前のプロファイルを削除する。</summary>
	/// <restriction>Admin</restriction>
	[Command("profiles? +delete +(\"?)(.+)\\1", AccessLevel.Admin, AccessLevel.Admin)]
	public static void ProfileDelete(string user, bool isWhisper, [Group(2)] string profileName)
	{
		var fileName = profileName.Replace(' ', '_');
		var profilePath = Path.Combine(ProfileHelper.ProfileFolder, $"{fileName}.json");
		if (!File.Exists(profilePath))
		{
			IRCConnection.SendMessage($"「{profileName}」というプロファイルは存在しません。");
			return;
		}

		ProfileHelper.Delete(fileName);
		if (TwitchPlaySettings.data.ProfileWhitelist.Contains(profileName))
		{
			TwitchPlaySettings.data.ProfileWhitelist.Remove(profileName);
			TwitchPlaySettings.WriteDataToFile();
		}

		IRCConnection.SendMessage($"プロファイル「{profileName}」を削除しました。",
			user, !isWhisper
		);
	}

	/// <name>Profile Disabled By</name>
	/// <syntax>profile disabled by [name]</syntax>
	/// <summary>プロファイルで無効になっているモジュールを表示する。</summary>
	[Command(@"profiles? +disabled +by +(.+)")]
	public static void ProfileDisabledBy([Group(1)] string profileName, string user, bool isWhisper) => ProfileWrapper(profileName, user, isWhisper, (filename, profileString) =>
	{
		var moduleIDs = ComponentSolverFactory.GetModuleInformation().Select(modInfo => modInfo.moduleID);
		var modules = ProfileHelper.GetProfile(filename).DisabledList.Where(modID => moduleIDs.Contains(modID));
		IRCConnection.SendMessage($"モジュールは、以下のプロファイルにより無効化されています：{profileString}: {modules.Join(", ")}");
	});

	/// <name>Module Enable/Disable</name>
	/// <syntax>module enable [name]\nmodule disable name</syntax>
	/// <summary>爆弾に表示されるモジュールの有効化/無効化を自動的に行う</summary>
	[Command(@"module (enable|disable) (.+)")]
	public static void ModuleToggle([Group(1)] string enableDisable, [Group(2)] string moduleQuery, string user, bool isWhisper) {
		if (!ComponentSolverFactory.GetModuleInformation().Search(moduleQuery, info => info.moduleDisplayName, out ModuleInformation moduleInfo, out string message)) {
			IRCConnection.SendMessage(message, user, !isWhisper);
			return;
		}

		string moduleID = moduleInfo.moduleID;
		if (!TwitchPlaySettings.data.ToggleableModules.Any(value => value == moduleInfo.moduleDisplayName || value == moduleID)) {
			IRCConnection.SendMessage($"モジュール「{moduleInfo.moduleDisplayName}」は変更できません。", user, !isWhisper);
			return;
		}

		// Create profile if it doesn't exist
		if (!File.Exists(ProfileHelper.GetPath("TP_Toggleable"))) {
			ProfileHelper.Write("TP_Toggleable", new HashSet<string>());
		}

		// Get existing profile
		HashSet<string> modules = ProfileHelper.GetProfile("TP_Toggleable").DisabledList;
		bool enable = enableDisable == "enable";
		bool enabled = !modules.Contains(moduleID);
		if (enable == enabled) {
			IRCConnection.SendMessage($"モジュール「{moduleInfo.moduleDisplayName}」は、すでに{(enable ? "有効化" : "無効化")}されています。", user, !isWhisper);
			return;
		}
		
		// Update profile based on the user's command
		if (enable) modules.Remove(moduleID);
		else modules.Add(moduleID);
		ProfileHelper.Write("TP_Toggleable", modules);
		IRCConnection.SendMessage($"モジュール「{moduleInfo.moduleDisplayName}」は{(enable ? "有効化" : "無効化")}されました。", user, !isWhisper);

		// Make sure the profile is active
		ProfileHelper.Enable("TP_Toggleable");
	}

	/// <name>Holdables</name>
	/// <syntax>holdables</syntax>
	/// <summary>利用可能な持ち上げ可能物を表示する。</summary>
	[Command(@"holdables")]
	public static void Holdables(string user, bool isWhisper) => IRCConnection.SendMessage("以下の持ち上げ可能物が存在します： {0}", user, !isWhisper, TwitchPlaysService.Instance.Holdables.Keys.Select(x => $"!{x}").Join(", "));

	/// <name>Disable Moderators</name>
	/// <syntax>disablemods</syntax>
	/// <summary>Disables all permission granted by the moderator rank.</summary>
	/// <restriction>Streamer</restriction>
	[Command(@"disablemods", AccessLevel.Streamer, AccessLevel.Streamer)]
	public static void DisableModerators()
	{
		UserAccess.ModeratorsEnabled = false;
		IRCConnection.SendMessage("すべてのモデレーターを一時的に無効にしました。");
	}

	/// <name>Enable Moderators</name>
	/// <syntax>enablemods</syntax>
	/// <summary>モデレーター権限を有効にする。</summary>
	/// <restriction>Streamer</restriction>
	[Command(@"enablemods", AccessLevel.Streamer, AccessLevel.Streamer)]
	public static void EnableModerators()
	{
		UserAccess.ModeratorsEnabled = true;
		IRCConnection.SendMessage("すべてのモデレーターを有効にしました。");
	}

	/// <name>Reload Data</name>
	/// <syntax>reloaddata</syntax>
	/// <summary>TPで使用されているすべてのデータ(設定、アクセス権限なぢ)を再読込する。</summary>
	/// <restriction>SuperUser</restriction>
	[Command("reloaddata", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static IEnumerator ReloadData(string user, bool isWhisper)
	{
		bool streamer = UserAccess.HasAccess(user, AccessLevel.Streamer);
		bool superuser = UserAccess.HasAccess(user, AccessLevel.SuperUser);

		TwitchPlaySettings.LoadDataFromFile();
		UserAccess.LoadAccessList();
		yield return ComponentSolverFactory.LoadDefaultInformation(true);
		if (TwitchPlaySettings.data.EnableAutoProfiles)
		{
			yield return ProfileHelper.LoadAutoProfiles();
		}

		if (streamer)
			UserAccess.AddUser(user, AccessLevel.Streamer);
		if (superuser)
			UserAccess.AddUser(user, AccessLevel.SuperUser);

		IRCConnectionManagerHoldable.TwitchPlaysDataRefreshed = true;
		IRCConnection.SendMessage("データを再読込しました。", user, !isWhisper);
	}

	/// <name>Reload Score Info</name>
	/// <syntax>reloadscoreinfo</syntax>
	/// <summary>すべてのモジュールのスコア情報を再読込する。</summary>
	/// <restriction>ScoringManager</restriction>
	[Command("reloadscoreinfo", AccessLevel.ScoringManager, AccessLevel.ScoringManager)]
	public static IEnumerator ReloadScoreInfo(string user, bool isWhisper)
	{
		yield return ComponentSolverFactory.LoadDefaultInformation(true);
		if (TwitchPlaySettings.data.EnableAutoProfiles)
		{
			yield return ProfileHelper.LoadAutoProfiles();
		}
		IRCConnection.SendMessage("スコア情報を再読込しました。", user, !isWhisper);
	}

	/// <name>Silence Mode</name>
	/// <syntax>silencemode</syntax>
	/// <summary>サイレントモードに切り替える。サイレントモードにすると、TPのチャットにメッセージを送れなくなる。</summary>
	/// <restriction>SuperUser</restriction>
	[Command(@"silencemode", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static void SilenceMode() => IRCConnection.ToggleSilenceMode();

	/// <name>Elevator</name>
	/// <syntax>elevator</syntax>
	/// <summary>エレベーターの現在の状態を表示する。</summary>
	/// <restriction>SuperUser</restriction>
	[Command(@"elevator", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static void Elevator() => TPElevatorSwitch.Instance?.ReportState();

	/// <name>Change Elevator</name>
	/// <syntax>elevator on\nelevator off\nelevator toggle</syntax>
	/// <summary>エレベーターの状態をオン/オフにする。</summary>
	[Command(@"elevator (on|off|flip|toggle|switch|press|push)")]
	public static IEnumerator Elevator([Group(1)] string command)
	{
		if (TPElevatorSwitch.Instance == null || TPElevatorSwitch.Instance.ElevatorSwitch == null || !TPElevatorSwitch.Instance.ElevatorSwitch.gameObject.activeInHierarchy)
			return null;

		var on = TPElevatorSwitch.IsON;
		switch (command)
		{
			case "on" when !TPElevatorSwitch.IsON:
			case "off" when TPElevatorSwitch.IsON:
			case "flip":
			case "toggle":
			case "switch":
			case "press":
			case "push":
				on = !on;
				break;
			case "on":
			case "off":
				TPElevatorSwitch.Instance.ReportState();
				return null;
		}

		return TPElevatorSwitch.Instance.ToggleSetupRoomElevatorSwitch(on);
	}

	private static readonly HashSet<string> confirming = new HashSet<string>();

	/// <name>Opt out</name>
	/// <syntax>optout</syntax>
	/// <summary>自信のランクとポイントを非表示にする。。</summary>
	[Command(@"opt[- ]?out")]
	public static void OptOut(string user)
	{
		if (!confirming.Contains(user))
		{
			confirming.Add(user);
			IRCConnection.SendMessage("本当に非表示にしますか？この操作は、やり直すことができません。承諾する場合は再度「!optout」を入力してください。");
			return;
		}

		confirming.Remove(user);

		Leaderboard.Instance.OptOut(user);
		IRCConnection.SendMessage($"{user}さんが非表示になりました。");
	}

	/// <name>Restart</name>
	/// <syntax>restart</syntax>
	/// <summary>ゲームを終了し、再起動する。</summary>
	/// <restriction>SuperUser</restriction>
	[Command("(?:restart|reboot)(?:game)?", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static void RestartGame()
	{
		if (SteamManager.Initialized) // ゲームがSteamを通じて起動した場合、Steamのサービスを使用して再起動する。
		{
			// このファイルを作成することで、ゲームがSteamのサービスを利用できるようになる。
			// このファイルは、ゲームが Steam を初期化しようとするまで削除されない。
			// 外部から削除状態はわからないため、TwitchPlaysServiceがこのファイルをSteamの初期化後に削除する。
			File.WriteAllText("steam_appid.txt", "341800");

			Process.Start(Process.GetCurrentProcess().MainModule.FileName, Environment.GetCommandLineArgs().Skip(1).Join());

			Application.Quit();
		}
		else
		{
			// boot.configファイルにsingle-instanceという引数があるため、ゲームは通常1つのインスタンスしか開くことができない。
			// これを回避するために、ファイルから引数を削除し、2つ目のインスタンスの起動後に元の内容を置き換える。

			string bootConfigPath = Path.Combine(Application.dataPath, "boot.config");
			string originalContents = File.ReadAllText(bootConfigPath);
			File.WriteAllText(bootConfigPath, originalContents.Replace("single-instance=", ""));

			Process
				.Start(Process.GetCurrentProcess().MainModule.FileName, Environment.GetCommandLineArgs().Skip(1).Join())
				.WaitForInputIdle(); // 元の内容に戻るのが早くなりすぎないように、ゲームが入力を受け付けるまで待つ。

			File.WriteAllText(bootConfigPath, originalContents);

			Application.Quit();
		}
	}

	/// <name>Quit</name>
	/// <syntax>quit</syntax>
	/// <summary>KTANEを終了する。</summary>
	/// <restriction>SuperUser</restriction>
	[Command("(?:quit|end)(?:game)?", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static void QuitGame() => SceneManager.Instance.Exit();

	/// <name>Check For Updates</name>
	/// <syntax>checkforupdates</syntax>
	/// <summary>Dropbox上にTPの新しいビルドがあるかどうかを確認する。DropboxバージョンのTPを使用している場合のみ適用する。</summary>
	/// <restriction>SuperUser</restriction>
	[Command("(?:checkforupdates?|cfu)", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static IEnumerator CheckForUpdates()
 	{
		yield return null;
		IRCConnection.SendMessage("自動アップデートは無効化されています。");
	}
 // 	{
	// 	yield return Updater.CheckForUpdates();

	// 	IRCConnection.SendMessage(Updater.UpdateAvailable ? "There is a new update to Twitch Plays!" : "Twitch Plays is up-to-date.");
	// }


	/// <name>Update</name>
	/// <syntax>update\nupdate force</syntax>
	/// <summary>利用可能なアップデートがある場合、それをインストールし、ゲームを再起動する。[force]を追加すると、利用可能なアップデートがあるかどうかに関係なくアップデートされる。DropboxバージョンのTPを使用している場合のみ適用される。</summary>
	/// <restriction>SuperUser</restriction>
	[Command("update(?:game|tp|twitchplays)?( force)?", AccessLevel.SuperUser, AccessLevel.SuperUser)]
 public static IEnumerator Update([Group(1)] bool force)
  	{
		yield return null;
		IRCConnection.SendMessage("自動アップデートは無効化されています。");
	}
	// public static IEnumerator Update([Group(1)] bool force) => Updater.Update(force);

	/// <name>Update</name>
	/// <syntax>revert</syntax>
	/// <summary>以前のアップデートに戻す。DropboxバージョンのTPを使用している場合のみ適用される。</summary>
	/// <restriction>SuperUser</restriction>
	[Command("revert(?:game|tp|twitchplays)?", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static IEnumerator Revert()
  	{
		yield return null;
		IRCConnection.SendMessage("自動アップデートは無効化されています。");
	}
// public static IEnumerator Revert() => Updater.Revert();

	/// <name>Reset Leaderboard</name>
	/// <syntax>leaderboard reset</syntax>
	/// <summary>リーダーボードのすべての情報を消去する。</summary>
	/// <restriction>SuperUser</restriction>
	[Command(@"leaderboard reset", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static void ResetLeaderboard(string user, bool isWhisper)
	{
		Leaderboard.Instance.ResetLeaderboard();
		IRCConnection.SendMessage("リーダーボードをリセットしました。", user, !isWhisper);
	}

	/// <name>Disable Whitelist</name>
	/// <syntax>disablewhitelist</syntax>
	/// <summary>ホワイトリストを無効にする。</summary>
	[Command(@"disablewhitelist", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static void DisableWhitelist()
	{
		TwitchPlaySettings.data.EnableWhiteList = false;
		TwitchPlaySettings.WriteDataToFile();
		TwitchPlaysService.Instance.UpdateUiHue();
		IRCConnection.SendMessage("ホワイトリストは無効になりました。");
	}

	/// <name>Enable Whitelist</name>
	/// <syntax>enablewhitelist</syntax>
	/// <summary>ホワイトリストを有効にする。</summary>
	/// <restriction>SuperUser</restriction>
	[Command(@"enablewhitelist", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static void EnableWhitelist()
	{
		TwitchPlaySettings.data.EnableWhiteList = true;
		TwitchPlaySettings.WriteDataToFile();
		TwitchPlaysService.Instance.UpdateUiHue();
		IRCConnection.SendMessage("ホワイトリストが有効になりました。");
	}

	/// <name>Mimic</name>
	/// <syntax>mimic [player] [command]</syntax>
	/// <summary>特定のコマンドを他のプレイヤーが実行したように見せかける。同じランクかそれ以下のプレイヤーに対してのみ機能する。</summary>
	/// <restriction>Admin</restriction>
	[Command(@"(?:issue|say|mimic)(?: ?commands?)?(?: ?as)? (\S+) (.+)", AccessLevel.Admin, AccessLevel.Admin)]
	public static void Mimic([Group(1)] string targetPlayer, [Group(2)] string newMessage, IRCMessage message)
	{
		targetPlayer = targetPlayer.FormatUsername();
		if (message.IsWhisper)
		{
			IRCConnection.SendMessage($"{message.UserNickName}さん：ウィスパーコメントでは、他のユーザーとしてコマンドを宣言することは許可されていません。", message.UserNickName, false);
			return;
		}

		if (UserAccess.HighestAccessLevel(message.UserNickName) < UserAccess.HighestAccessLevel(targetPlayer))
		{
			IRCConnection.SendMessage($"{message.UserNickName}さん：あなたの権限が低いため、{targetPlayer}としてコマンドを宣言できません。");
			return;
		}

		IRCConnection.ReceiveMessage(targetPlayer, message.UserColorCode, newMessage);
	}

	/// <name>Skip Command</name>
	/// <syntax>skipcommand</syntax>
	/// <summary>強制的に実行中のコマンドをスキップする。スタックしているコマンドをスキップするためにのみ使用することを推奨する。これは問題を引き起こす可能性があるため、使用には注意が必要である。</summary>
	/// <restriction>Admin</restriction>
	[Command("skip(?:coroutine|command|cmd)?", AccessLevel.Admin, AccessLevel.Admin)]
	public static void Skip()
	{
		TwitchPlaysService.Instance.CoroutineQueue.SkipCurrentCoroutine = true;
	}

	/// <name>Run as</name>
	/// <syntax>runas [color] [username]#[discrminator] [command]</syntax>
	/// <summary>コマンドを特定のDiscordユーザーとして実行する。Discord Playsのみ使用可能。</summary>
	/// <restriction>Streamer</restriction>
	[Command(@"runas (#.{6}) (.*?)#([0-9]{4}) (!.*?)$", AccessLevel.Streamer, AccessLevel.Streamer)]
	public static void RunCommandAs([Group(1)] string color, [Group(2)] string username, [Group(3)] string discriminator, [Group(4)] string command) =>
		IRCConnection.ReceiveMessage($"{username}#{discriminator}", color, command);

	//現在のところ、デバッグ・コマンドはstreamerのみ使用できる。(whispertestはsuperuser以上で使用可能)
	[Command("whispertest", AccessLevel.SuperUser, AccessLevel.SuperUser), DebuggingOnly]
	public static void WhisperTest(string user) => IRCConnection.SendMessage("テスト成功", user, false);

	[Command("secondary camera", AccessLevel.Streamer, AccessLevel.Streamer), DebuggingOnly]
	public static void EnableSecondaryCamera() => GameRoom.ToggleCamera(false);

	[Command("main camera", AccessLevel.Streamer, AccessLevel.Streamer), DebuggingOnly]
	public static void EnableMainCamera() => GameRoom.ToggleCamera(true);

	[Command(@"(move|rotate) ?camera ?([xyz]) (-?[0-9]+(?:\\.[0-9]+)*)", AccessLevel.Streamer, AccessLevel.Streamer), DebuggingOnly]
	public static void ChangeCamera([Group(1)] string action, [Group(2)] string axis, [Group(3)] float number, string user, bool isWhisper)
	{
		if (GameRoom.IsMainCamera)
		{
			IRCConnection.SendMessage("二次カメラを動かす前に、「!secondary camera」で二次カメラに切り替えてください。", user, !isWhisper);
			return;
		}

		Vector3 vector = new Vector3();
		switch (axis)
		{
			case "x": vector = new Vector3(number, 0, 0); break;
			case "y": vector = new Vector3(0, number, 0); break;
			case "z": vector = new Vector3(0, 0, number); break;
		}

		switch (action)
		{
			case "move": GameRoom.MoveCamera(vector); break;
			case "rotate": GameRoom.RotateCamera(vector); break;
		}

		CameraChanged(user, isWhisper);
	}

	[Command("reset ?camera", AccessLevel.Streamer, AccessLevel.Streamer), DebuggingOnly]
	public static void ResetCamera(string user, bool isWhisper)
	{
		GameRoom.ResetCamera();
		CameraChanged(user, isWhisper);
	}

	[Command(null)]
	public static bool DefaultCommand(string cmd, string user, bool isWhisper)
	{
		if (!TwitchPlaySettings.data.GeneralCustomMessages.ContainsKey(cmd.ToLowerInvariant()))
			return
				TwitchPlaySettings.data.IgnoreCommands
					.Contains(cmd.ToLowerInvariant()); //Ignore the command if it's in IgnoreCommands
		IRCConnection.SendMessage(TwitchPlaySettings.data.GeneralCustomMessages[cmd.ToLowerInvariant()], user, !isWhisper);
		return true;
	}

	#region Private methods
	private static void SetGameMode(TwitchPlaysMode mode, bool toggle, bool on, string user, bool isWhisper, bool enabledForEveryone, string disabledMessage)
	{
		if (!UserAccess.HasAccess(user, AccessLevel.Mod, true) && !enabledForEveryone && !TwitchPlaySettings.data.AnarchyMode)
		{
			IRCConnection.SendMessage(string.Format(disabledMessage, user), user, !isWhisper);
			return;
		}

		if (toggle)
			OtherModes.Toggle(mode);
		else
			OtherModes.Set(mode, on);
		IRCConnection.SendMessage($"次のゲームは{OtherModes.GetName(OtherModes.nextMode)}モードになります。", user, !isWhisper);
	}

	private static void ShowRank(Leaderboard.LeaderboardEntry entry, string targetUser, string user, bool isWhisper, bool numeric = false) => ShowRank(entry == null ? null : new[] { entry }, targetUser, user, isWhisper, numeric);

	private static void ShowRank(IList<Leaderboard.LeaderboardEntry> entries, string targetUser, string user, bool isWhisper, bool numeric = false)
	{
		if (entries != null)
		{
			entries = entries.Where(entry => entry != null).ToList();
			if (entries.Count == 0)
			{
				entries = null;
			}
		}

		if (entries == null && numeric)
			IRCConnection.SendMessage(TwitchPlaySettings.data.RankTooLow, user, !isWhisper);
		else if (entries == null)
			IRCConnection.SendMessage(string.Format(TwitchPlaySettings.data.DoYouEvenPlayBro, targetUser), user, !isWhisper);
		else
		{
			foreach (var entry in entries)
			{
				string txtSolver = "";
				string txtSolo = "";
				if (entry.TotalSoloClears > 0)
				{
					var recordTimeSpan = TimeSpan.FromSeconds(entry.RecordSoloTime);
					txtSolver = TwitchPlaySettings.data.SolverAndSolo;
					txtSolo = string.Format(TwitchPlaySettings.data.SoloRankQuery, entry.SoloRank, (int) recordTimeSpan.TotalMinutes, recordTimeSpan.Seconds);
				}
				IRCConnection.SendMessage(string.Format(TwitchPlaySettings.data.RankQuery, entry.UserName, entry.OptOut ? "--" : entry.Rank.ToString(), entry.SolveCount, entry.StrikeCount, txtSolver, txtSolo, entry.OptOut ? "--" : entry.SolveScore.ToString("0.##")), user, !isWhisper);
			}
		}
	}

	private static int GetMaximumModules(KMGameInfo inf, int maxAllowed = int.MaxValue) => Math.Min(TPElevatorSwitch.IsON ? 54 : inf.GetMaximumBombModules(), maxAllowed);

	private static string ResolveMissionID(KMGameInfo inf, string targetID, out string failureMessage)
	{
		failureMessage = null;
		var missions = ModManager.Instance.ModMissions;

		var mission = missions.Find(x => x.name.EqualsIgnoreCase(targetID)) ??
			missions.Find(x => Regex.IsMatch(x.name, $"^mod_.+_{Regex.Escape(targetID)}", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase));
		if (mission == null)
		{
			failureMessage = $"「{targetID}」というIDのミッションは存在しません。";
			return null;
		}

		var availableMods = inf.GetAvailableModuleInfo().Where(x => x.IsMod).Select(y => y.ModuleId).ToList();
		if (MultipleBombs.Installed())
			availableMods.Add("Multiple Bombs");
		var missingMods = new HashSet<string>();
		var modules = ComponentSolverFactory.GetModuleInformation().ToList();

		var generatorSetting = mission.GeneratorSetting;
		var componentPools = generatorSetting.ComponentPools;
		int moduleCount = 0;
		foreach (var componentPool in componentPools)
		{
			moduleCount += componentPool.Count;
			var modTypes = componentPool.ModTypes;
			if (modTypes == null || modTypes.Count == 0) continue;
			foreach (string mod in modTypes.Where(x => !availableMods.Contains(x)))
			{
				missingMods.Add(modules.Find(x => x.moduleID == mod)?.moduleDisplayName ?? mod);
			}
		}
		if (missingMods.Count > 0)
		{
			failureMessage = $"「{targetID}」というミッションが見つかりましたが、以下のMODがインストール/読み込みされていません：{string.Join(", ", missingMods.OrderBy(x => x).ToArray())}";
			return null;
		}
		if (moduleCount > GetMaximumModules(inf))
		{
			failureMessage = TPElevatorSwitch.IsON
				? $"「{targetID}」というミッションが見つかりましたが、エレベーターで使用可能なモジュール数を超えています。"
				: $"「{targetID}」というミッションが見つかりましたが、{moduleCount}個のモジュールを搭載できる爆弾ケースが現在インストール/読み込みされていません。";
			return null;
		}

		return mission.name;
	}

	private static IEnumerator RunWrapper(string user, bool isWhisper, Func<IEnumerator> action, bool VSOnly = false)
	{
		yield return null;
		if (TwitchPlaysService.Instance.CurrentState != KMGameInfo.State.PostGame && TwitchPlaysService.Instance.CurrentState != KMGameInfo.State.Setup)
		{
			IRCConnection.SendMessage("現在「!run」コマンドを使用できません。");
			yield break;
		}

		if (VSOnly && !OtherModes.VSModeOn)
		{
			IRCConnection.SendMessage("この「!run」コマンドはVSモードのみ実行できます。");
			yield break;
		}

		if (!((TwitchPlaySettings.data.EnableRunCommand && (!TwitchPlaySettings.data.EnableWhiteList || UserAccess.HasAccess(user, AccessLevel.Defuser, true))) || UserAccess.HasAccess(user, AccessLevel.Mod, true) || TwitchPlaySettings.data.AnarchyMode) || isWhisper)
		{
			IRCConnection.SendMessageFormat(TwitchPlaySettings.data.RunCommandDisabled, user);
			yield break;
		}
		yield return action();
	}

	private static void ProfileWrapper(string profileName, string user, bool isWhisper, Action<string, string> action)
	{
		if (TwitchPlaysService.Instance.CurrentState != KMGameInfo.State.PostGame
			&& TwitchPlaysService.Instance.CurrentState != KMGameInfo.State.Setup
			&& TwitchPlaysService.Instance.CurrentState != KMGameInfo.State.Gameplay)
		{
			IRCConnection.SendMessage("現在「!profile」コマンドを使用できません。");
			return;
		}

		var profileString = ProfileHelper.GetProperProfileName(profileName);
		if (TwitchPlaySettings.data.ProfileWhitelist.Contains(profileString))
			action(profileString.Replace(' ', '_'), profileString);
		else
			IRCConnection.SendMessage(string.Format(TwitchPlaySettings.data.ProfileNotWhitelisted, profileName), user, !isWhisper);
	}

	private static IEnumerator RunDistribution(string user, int modules, KMGameInfo inf, ModuleDistributions distribution)
	{
		if (!distribution.Enabled && !UserAccess.HasAccess(user, AccessLevel.Mod, true) && !TwitchPlaySettings.data.AnarchyMode)
		{
			IRCConnection.SendMessage($"「{distribution.DisplayName}」という分配は無効化されています。");
			return null;
		}

		if (modules < distribution.MinModules)
		{
			IRCConnection.SendMessage($"分配「{distribution.DisplayName}」の最小のモジュール数は{distribution.MinModules}です。");
			return null;
		}

		int maxModules = GetMaximumModules(inf, distribution.MaxModules);
		if (modules > maxModules && !UserAccess.HasAccess(user, AccessLevel.Mod, true))
		{
			if (modules > distribution.MaxModules)
				IRCConnection.SendMessage($"分配「{distribution.DisplayName}」の最大のモジュール数は{distribution.MaxModules}です。");
			else
				IRCConnection.SendMessage($"最大のモジュール数は{maxModules}\"です。");
			return null;
		}

		var mission = ScriptableObject.CreateInstance<KMMission>();
		mission.PacingEventsEnabled = TwitchPlaySettings.data.PacingEventsOnRunBomb;
		mission.DisplayName = modules + " " + distribution.DisplayName;
		mission.Description = modules + " " + distribution.DisplayName;
		try
		{
			mission.GeneratorSetting = distribution.GenerateMission(modules, OtherModes.TimeModeOn, out int rewardPoints);
			rewardPoints = (rewardPoints * OtherModes.ScoreMultiplier).RoundToInt();
			TwitchPlaySettings.SetRewardBonus(rewardPoints);
			IRCConnection.SendMessage("爆弾を解除した時の報酬: " + rewardPoints);
		}
		catch (InvalidOperationException e)
		{
			IRCConnection.SendMessage($"分配「{distribution.DisplayName}」を実行できません：{e.Message}");
			return null;
		}

		return RunMissionCoroutine(mission);
	}

	private static IEnumerator RunMissionCoroutine(KMMission mission, string seed = "-1")
	{
		if (TwitchPlaysService.Instance.CurrentState == KMGameInfo.State.PostGame)
		{
			// Press the “back” button
			var e = PostGameCommands.Continue();
			while (e.MoveNext())
				yield return e;

			// Wait until we’re back in the setup room
			yield return new WaitUntil(() => TwitchPlaysService.Instance.CurrentState == KMGameInfo.State.Setup);
		}

		TwitchPlaysService.Instance.GetComponent<KMGameCommands>().StartMission(mission, seed);
		OtherModes.RefreshModes(KMGameInfo.State.Transitioning);
	}

	private static IEnumerator RunMissionCoroutine(string missionId, string seed = "-1")
	{
		if (TwitchPlaysService.Instance.CurrentState == KMGameInfo.State.PostGame)
		{
			// Press the “back” button
			var e = PostGameCommands.Continue();
			while (e.MoveNext())
				yield return e;

			// Wait until we’re back in the setup room
			yield return new WaitUntil(() => TwitchPlaysService.Instance.CurrentState == KMGameInfo.State.Setup);
		}

		TwitchPlaysService.Instance.GetComponent<KMGameCommands>().StartMission(missionId, seed);
		OtherModes.RefreshModes(KMGameInfo.State.Transitioning);
	}

	private static void CameraChanged(string user, bool isWhisper)
	{
		Transform camera = GameRoom.SecondaryCamera.transform;

		DebugHelper.Log($"Camera Position = {Math.Round(camera.localPosition.x, 3)},{Math.Round(camera.localPosition.y, 3)},{Math.Round(camera.localPosition.z, 3)}");
		DebugHelper.Log($"Camera Euler Angles = {Math.Round(camera.localEulerAngles.x, 3)},{Math.Round(camera.localEulerAngles.y, 3)},{Math.Round(camera.localEulerAngles.z, 3)}");
		IRCConnection.SendMessage($"カメラの位置 = {Math.Round(camera.localPosition.x, 3)},{Math.Round(camera.localPosition.y, 3)},{Math.Round(camera.localPosition.z, 3)}, カメラのオイラー角 = {Math.Round(camera.localEulerAngles.x, 3)},{Math.Round(camera.localEulerAngles.y, 3)},{Math.Round(camera.localEulerAngles.z, 3)}", user, !isWhisper);
	}
	#endregion
}
