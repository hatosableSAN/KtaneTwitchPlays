using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Props;
using UnityEngine;

/// <summary>ゲーム中のコマンド</summary>
static class GameCommands
{
	public static List<IRCMessage> calledCommands = new List<IRCMessage>();

	#region Commands during the game
	/// <name>Cancel</name>
	/// <syntax>cancel</syntax>
	/// <summary>現在実行中のコマンドをキャンセルする。</summary>
	[Command(@"cancel")]
	public static void Cancel() => CoroutineCanceller.SetCancel();

	/// <name>Stop</name>
	/// <syntax>stop</syntax>
	/// <summary>現在のキュー追加済みコマンドを停止する。</summary>
	[Command(@"stop")]
	public static void Stop() => TwitchGame.Instance.StopCommands();

	/// <name>Get Notes</name>
	/// <syntax>notes[note]</syntax>
	/// <summary>メモの内容をチャットに送る。</summary>
	/// <argument name="note">The note's number.</argument>
	[Command(@"notes(-?\d+)")]
	public static void ShowNotes([Group(1)] int index, string user, bool isWhisper) =>
		IRCConnection.SendMessage(TwitchPlaySettings.data.Notes, user, !isWhisper, index, TwitchGame.Instance.NotesDictionary.TryGetValue(index - 1, out var note) ? note : TwitchPlaySettings.data.NotesSpaceFree);

	/// <name>Set Notes</name>
	/// <syntax>notes[note] [contents]</syntax>
	/// <summary>メモを記入する。</summary>
	/// <argument name="note">The note's number.</argument>
	/// <argument name="contents">New text of the note.</argument>
	[Command(@"notes(-?\d+) +(.+)")]
	public static void SetNotes([Group(1)] int index, [Group(2)] string notes, string user, bool isWhisper)
	{
		IRCConnection.SendMessage(string.Format(TwitchPlaySettings.data.NotesTaken, index, notes), user, !isWhisper);
		index--;
		TwitchGame.Instance.NotesDictionary[index] = notes;
		TwitchGame.ModuleCameras?.SetNotes();
	}

	/// <name>Append Notes</name>
	/// <syntax>notes[note]append [contents]</syntax>
	/// <summary>メモの末尾に追加する。</summary>
	/// <argument name="note">The note's number.</argument>
	/// <argument name="contents">The text that will be appended to the note.</argument>
	[Command(@"notes(-?\d+)append +(.+)")]
	public static void SetNotesAppend([Group(1)] int index, [Group(2)] string notes, string user, bool isWhisper)
	{
		IRCConnection.SendMessage(string.Format(TwitchPlaySettings.data.NotesAppended, index, notes), user, !isWhisper);
		index--;
		if (TwitchGame.Instance.NotesDictionary.ContainsKey(index))
			TwitchGame.Instance.NotesDictionary[index] += " " + notes;
		else
			TwitchGame.Instance.NotesDictionary[index] = notes;
		TwitchGame.ModuleCameras?.SetNotes();
	}

	/// <name>Clear Notes</name>
	/// <syntax>notes[note]clear</syntax>
	/// <summary>メモの中身を削除する。</summary>
	/// <argument name="note">The note's number.</argument>
	[Command(@"(?:notes(-?\d+)clear|clearnotes(-?\d+))")]
	public static void SetNotesClear([Group(1)] int index, string user, bool isWhisper)
	{
		IRCConnection.SendMessage(string.Format(TwitchPlaySettings.data.NoteSlotCleared, index), user, !isWhisper);
		index--;
		if (TwitchGame.Instance.NotesDictionary.ContainsKey(index))
			TwitchGame.Instance.NotesDictionary.Remove(index);
		TwitchGame.ModuleCameras?.SetNotes();
	}

	/// <name>Snooze</name>
	/// <syntax>snooze</syntax>
	/// <summary>アラームを一時停止する。</summary>
	[Command(@"snooze")]
	public static IEnumerator Snooze()
	{
		if (GameRoom.Instance is ElevatorGameRoom)
			yield break;
		if (!TwitchPlaysService.Instance.Holdables.TryGetValue("alarm", out var alarmClock))
			yield break;

		var e = alarmClock.Hold();
		while (e.MoveNext())
			yield return e.Current;

		e = AlarmClockCommands.Snooze(alarmClock.Holdable.GetComponent<AlarmClock>());
		while (e.MoveNext())
			yield return e.Current;
	}

	/// <name>Show Claims</name>
	/// <syntax>claims [user]</syntax>
	/// <summary>あるユーザーのモジュール割り当てを見る。</summary>
	/// <argument name="user">The user whose claims you want to see.</argument>
	[Command(@"claims +(.+)")]
	public static void ShowClaimsOfAnotherPlayer([Group(1)] string targetUser, string user, bool isWhisper)
	{
		if (TwitchPlaySettings.data.AnarchyMode)
			IRCConnection.SendMessage($"@{user}さん：モジュールの割り当てはアナーキーモードでは無効化されています。", user, !isWhisper);
		else if (isWhisper && TwitchPlaySettings.data.EnableWhispers)
			IRCConnection.SendMessage("ウィスパーコメントでは、他人の割り当てを確認することはできません。", user, false);
		else
			ShowClaimsOfUser(targetUser, isWhisper, TwitchPlaySettings.data.OwnedModuleListOther, TwitchPlaySettings.data.NoOwnedModulesOther);
	}

	/// <name>Claims</name>
	/// <syntax>claims</syntax>
	/// <summary>自分のモジュール割り当てを見る。</summary>
	[Command(@"claims")]
	public static void ShowClaims(string user, bool isWhisper)
	{
		if (TwitchPlaySettings.data.AnarchyMode)
			IRCConnection.SendMessage($"@{user}さん：モジュールの割り当てはアナーキーモードでは無効化されています。", user, !isWhisper);
		else
			ShowClaimsOfUser(user, isWhisper, TwitchPlaySettings.data.OwnedModuleList, TwitchPlaySettings.data.NoOwnedModules);
	}

	/// <name>Claim View Pin</name>
	/// <syntax>claim (what)\nview (what)\npin (what)\n(actions) (what)</syntax>
	/// <summary>モジュールをまとめて割り当て、表示、ピン留めする。(actions) は、claim, view、pin のいずれかをスペースで分けて入力する。</summary>
	/// <argument name="actions">A combination of claim, view or pin seperated by spaces.</argument>
	/// <argument name="what">アクションを実行するモジュールコードのリスト。すべての未解除モジュールに対してアクションを実行するには、「all」を指定する。</argument>
	[Command(@"((?:claim *|view *|pin *)+)(?: +(.+)| *(all))")]
	public static void ClaimViewPin(string user, bool isWhisper, [Group(1)] string command, [Group(2)] string claimWhat, [Group(3)] bool all)
	{
		var strings = all ? null : claimWhat.SplitFull(' ', ',', ';');
		var modules =
			all ? TwitchGame.Instance.Modules.Where(m => !m.Solved && !m.Hidden).ToArray() :
			strings.Length == 0 ? null :
			TwitchGame.Instance.Modules.Where(md => strings.Any(str => str.EqualsIgnoreCase(md.Code)) && !md.Hidden).ToArray();

		if (modules == null || modules.Length == 0)
		{
			IRCConnection.SendMessage($"@{user}さん：そのようなモジュールはありません。", user, !isWhisper);
			return;
		}
		ClaimViewPin(user, isWhisper, modules, command.Contains("claim"), command.Contains("view"), command.Contains("pin"));
	}

	/// <name>Claim Any</name>
	/// <syntax>claim [source]\nclaim [source] view</syntax>
	/// <summary>未解除かつ未割り当てのモジュールをランダムに割り当てる。</summary>
	/// <argument name="source">The source of the modules to pick from. any for any module, van for vanilla and mod for modded modules.</argument>
	[Command(@"(?:claim *(any|van|mod) *(view)?|(view) *claim *(any|van|mod))")]
	public static void ClaimAny([Group(1)] string claimWhat1, [Group(4)] string claimWhat2, [Group(2)] bool view1, [Group(3)] bool view2, string user, bool isWhisper)
	{
		var claimWhat = claimWhat1 ?? claimWhat2;

		var vanilla = claimWhat.EqualsIgnoreCase("van");
		var modded = claimWhat.EqualsIgnoreCase("mod");
		var view = view1 || view2;
		var avoid = new[] { "Forget Everything", "Forget Me Not", "Souvenir", "The Swan", "The Time Keeper", "Turn The Key", "Turn The Keys" };

		var unclaimed = TwitchGame.Instance.Modules
			.Where(module => (vanilla ? !module.IsMod : !modded || module.IsMod) && !module.Claimed && !module.Solved && !module.Hidden && !avoid.Contains(module.HeaderText) && GameRoom.Instance.IsCurrentBomb(module.BombID))
			.Shuffle()
			.FirstOrDefault();

		if (unclaimed != null)
			ClaimViewPin(user, isWhisper, new[] { unclaimed }, claim: true, view: view);
		else
			IRCConnection.SendMessage($"割り当てられていない{(vanilla ? "バニラ" : modded ? "MOD" : null)}モジュールはありません。");
	}

	/// <name>Unclaim All</name>
	/// <syntax>unclaim all\nunclaim queued</syntax>
	/// <summary>割り当て済み/割り当てのキュー追加済みであるモジュールの割り当てを全解除する。unclaim queuedの場合、割り当てのキュー追加済みであるモジュールのみを解除する。</summary>
	[Command(@"(?:unclaim|release) *(?:all|(q(?:ueued?)?))")]
	public static void UnclaimAll(string user, [Group(1)] bool queuedOnly)
	{
		foreach (var module in TwitchGame.Instance.Modules)
		{
			module.RemoveFromClaimQueue(user);
			// Only unclaim the player’s own modules. Avoid releasing other people’s modules if the user is a moderator.
			if (!module.Solved && !queuedOnly && module.PlayerName == user)
				module.SetUnclaimed();
		}
	}

	/// <name>Unclaim Specific</name>
	/// <syntax>unclaim [what]</syntax>
	/// <summary>モジュールの割り当てをまとめて解除する。</summary>
	/// <argument name="what">A list of module codes to unclaim.</argument>
	[Command(@"(?:unclaim|release) +(.+)")]
	public static void UnclaimSpecific([Group(1)] string unclaimWhat, string user, bool isWhisper)
	{
		var strings = unclaimWhat.SplitFull(' ', ',', ';');
		var modules = strings.Length == 0 ? null : TwitchGame.Instance.Modules.Where(md => !md.Solved && !md.Hidden && md.PlayerName == user && strings.Any(str => str.EqualsIgnoreCase(md.Code))).ToArray();
		if (modules == null || modules.Length == 0)
		{
			IRCConnection.SendMessage($"@{user}さん：そのようなモジュールはありません。", user, !isWhisper);
			return;
		}

		foreach (var module in modules)
			module.SetUnclaimed();
	}

	public static List<TwitchModule> unclaimedModules;
	public static int unclaimedModuleIndex;
	/// <name>Unclaimed</name>
	/// <syntax>unclaimed</syntax>
	/// <summary>未割り当てのモジュールを最大3つ表示する。</summary>
	[Command(@"unclaimed")]
	public static void ListUnclaimed(string user, bool isWhisper)
	{
		// TwitchGame sets up the unclaimed list at the beginning of each round, so it would be hard to hit this but just in case someone does we can't do anything until they're setup.
		if (unclaimedModules == null)
		{
			return;
		}

		void checkAndWrap()
		{
			// We've reached the end, wrap back to the beginning.
			if (unclaimedModuleIndex >= unclaimedModules.Count)
			{
				// Add back any modules that may have been released.	
				unclaimedModules = TwitchGame.Instance.Modules.Where(h => h.CanBeClaimed && !h.Claimed && !h.Solved && !h.Hidden && Votes.voteModule != h && !h.Votesolving)
					.Shuffle().ToList();
				unclaimedModuleIndex = 0;
			}
		}

		// The for loop below won't run if all the modules got claimed, but we still need to add back in any modules that were released.
		if (unclaimedModules.Count == 0)
			checkAndWrap();

		List<string> unclaimed = new List<string>();
		for (int i = 0; i < 3 && i < unclaimedModules.Count; i++) // In case there are less than 3 modules, we have to lower the amount we return so we don't show repeats.
		{
			// See if there is a valid module at the current index and increment for the next go around.
			TwitchModule handle = unclaimedModules[unclaimedModuleIndex];
			if (!handle.CanBeClaimed || handle.Claimed || handle.Solved || handle.Hidden || Votes.voteModule == handle || handle.Votesolving)
			{
				unclaimedModules.RemoveAt(unclaimedModuleIndex);
				i--;

				// Check if there aren't any unclaimed modules left.
				if (unclaimedModules.Count == 0)
				{
					break;
				}

				// Wrap in case we removed the last module.
				checkAndWrap();
				continue;
			}

			// At this point we have a valid module, so we should increase the index and wrap.
			unclaimedModuleIndex++;
			checkAndWrap();

			string moduleString = string.Format($"{handle.TranslatedName} ({handle.HeaderText}, {handle.Code})");
			// If we hit a duplicate because we were at the end of the list and we wrapped so we'll skip over it and get another item.
			if (unclaimed.Contains(moduleString))
			{
				i--;

				continue;
			}

			unclaimed.Add(moduleString);
		}

		// If we didn't find any unclaimed, there aren't any left.
		if (unclaimed.Count == 0)
		{
			IRCConnection.SendMessage(string.Format(TwitchPlaySettings.data.NoUnclaimed, user), user, !isWhisper);
			return;
		}

		IRCConnection.SendMessage($"未割り当てのモジュール: {unclaimed.Join(", ")}");
	}

	/// <name>Unsolved</name>
	/// <syntax>unsolved</syntax>
	/// <summary>未解除のモジュールを最大3つ表示する。</summary>
	[Command(@"unsolved")]
	public static void ListUnsolved(string user, bool isWhisper)
	{
		if (TwitchGame.Instance.Bombs.All(b => b.IsSolved))
		{
			// If the command is issued while the winning fanfare is playing.
			IRCConnection.SendMessage("爆弾は既に解除されました!", user, !isWhisper);
			return;
		}

		IEnumerable<string> unsolved = TwitchGame.Instance.Modules
			.Where(module => !module.Solved && GameRoom.Instance.IsCurrentBomb(module.BombID) && !module.Hidden)
			.Shuffle().Take(3)
			.Select(module => $"{module.TranslatedName} ({module.HeaderText}, {module.Code}) - {(module.PlayerName == null ? "割り当てなし" : $"{module.PlayerName}に割り当て")}")
			.ToList();

		IRCConnection.SendMessage(unsolved.Any() ? $"未解除のモジュール: {unsolved.Join(", ")}" : "現在未解除で公開されているモジュールはありません。", user, !isWhisper);
	}

	/// <name>Find Claim View</name>
	/// <syntax>find (actions) [what]</syntax>
	/// <summary>モジュール名(英名)に基づいてモジュールを検索する。カンマ、セミコロンで分割して複数検索(OR検索)も可能である。findの後に行動を入れて、一致したモジュールに対してアクションを行うことも出来る。</summary>
	/// <argument name="actions">A combination of claim or view seperated by spaces.</argument>
	/// <argument name="what">Partial module names seperated by commas or semicolons.</argument>
	[Command(@"(?:find|search)((?: *claim| *view)*) +(.+)")]
	public static void FindClaimView([Group(1)] string commands, [Group(2)] string queries, string user, bool isWhisper)
	{
		var claim = commands.ContainsIgnoreCase("claim");
		var view = commands.ContainsIgnoreCase("view");

		var terms = queries.SplitFull(',', ';').Select(q => q.Trim()).Distinct().ToArray();
		if (terms.Length > TwitchPlaySettings.data.FindClaimTerms && claim && TwitchPlaySettings.data.FindClaimTerms != -1)
		{
			IRCConnection.SendMessageFormat("@{0}さん：検索の単語リストの大きさを{1}以下にしてください。", user, TwitchPlaySettings.data.FindClaimTerms); // Prevents lists greater than length of 3 while using !claim
			return;
		}

		var modules = FindModules(terms).ToList();
		if (modules.Count == 0)
		{
			IRCConnection.SendMessage("そのようなモジュールはありません。", user, !isWhisper);
			return;
		}

		if (claim)
		{
			if (!TwitchGame.Instance.FindClaimEnabled && !OtherModes.TrainingModeOn)
			{
				IRCConnection.SendMessageFormat("@{0}さん：findclaimコマンドは爆弾開始から{1}秒間は利用できません。", user, TwitchPlaySettings.data.FindClaimDelay); // Prevents findclaim spam at the start of a bomb
				return;
			}
			if (!TwitchGame.Instance.FindClaimPlayers.ContainsKey(user)) TwitchGame.Instance.FindClaimPlayers.Add(user, 0);

			var _prevClaims = TwitchGame.Instance.FindClaimPlayers[user];
			var _allowedClaims = TwitchGame.Instance.FindClaimUse;
			var _remainingClaims = _allowedClaims - _prevClaims;

			if (_remainingClaims < 1 && TwitchPlaySettings.data.FindClaimLimit != -1)
			{
				IRCConnection.SendMessageFormat("@{0}さん：findclaimの上限に達しました。", user);
				return;
			}

			if (modules.Count > _remainingClaims && TwitchPlaySettings.data.FindClaimLimit != -1)
			{
				IRCConnection.SendMessageFormat("@{0}さん：findclaimの上限である{1}に達しました。最初の{2}つの割り当てのみ行います。", user, _allowedClaims, _remainingClaims);
				ClaimViewPin(user, isWhisper, modules.Take(_remainingClaims), claim: claim, view: view);
			}
			else ClaimViewPin(user, isWhisper, modules, claim: claim, view: view);

			TwitchGame.Instance.FindClaimPlayers[user] = _prevClaims + modules.Count;
		}
		else if (view) ClaimViewPin(user, isWhisper, modules, claim: claim, view: view);
		else
			// Neither claim nor view: just “find”, so output top 3 search results
			IRCConnection.SendMessage("{0}さん：{2}個のモジュールが該当しました：{1}", user, !isWhisper, user,
				modules.Take(3).Select(handle =>
					$"{handle.HeaderText} ({handle.Code}) - {(handle.Solved ? "解除済み" : handle.PlayerName == null ? "未割り当て" : handle.PlayerName+"に割り当て済")}").Join(", "),
				modules.Count);
    IRCConnection.SendMessage("和名を調べるには、「!x help」コマンドを使用してください。");
	}

	/// <name>Find Player</name>
	/// <syntax>findplayer [what]</syntax>
	/// <summary>そのモジュールを割り当てられている人を検索する。[what]には、カンマやセミコロンで区切ってモジュール名(部分検索OK)を指定できる。</summary>
	/// <argument name="what">A combination of claim or view seperated by spaces.</argument>
	[Command(@"(?:find *player|player *find|search *player|player *search) +(.+)", AccessLevel.User, /* Disabled in Anarchy mode */ AccessLevel.Streamer)]
	public static void FindPlayer([Group(1)] string queries, string user, bool isWhisper)
	{
		List<string> modules = FindModules(queries.SplitFull(',', ';').Select(q => q.Trim()).Distinct().ToArray(), m => m.PlayerName != null)
			.Select(module => $"{module.TranslatedName} ({module.HeaderText}, {module.Code})：{module.PlayerName}に割り当て")
			.ToList();
		IRCConnection.SendMessage(modules.Count > 0 ? $"モジュール： {modules.Join(", ")}" : "そのような解除された/割り当てられたモジュールは存在しません。", user, !isWhisper);
	}

	/// <name>Find Solved</name>
	/// <syntax>findsolved [what]</syntax>
	/// <summary>そのモジュール解除した人を検索する。[what]には、カンマやセミコロンで区切ってモジュール名(部分検索OK)を指定できる。</summary>
	/// <argument name="what">A combination of claim or view seperated by spaces.</argument>
	[Command(@"(?:find *solved|solved *find|search *solved|solved *search) +(.+)", AccessLevel.User, /* Disabled in Anarchy mode */ AccessLevel.Streamer)]
	public static void FindSolved([Group(1)] string queries, string user, bool isWhisper)
	{
		List<string> modules = FindModules(queries.SplitFull(',', ';').Select(q => q.Trim()).Distinct().ToArray(), m => m.Solved)
			.Select(module => $"{module.HeaderText} ({module.Code})：{module.PlayerName}さんに割り当て済み")
			.ToList();
		IRCConnection.SendMessage(modules.Count > 0 ? $"モジュール: {modules.Join(", ")}" : "そのようなモジュールはありません。", user, !isWhisper);
	}

	/// <name>Find Duplicate</name>
	/// <syntax>findduplicate (what)</syntax>
	/// <summary>重複しているモジュールを検索する。[what]には、カンマやセミコロンで区切ってモジュール名(部分検索OK)を指定できる。指定がない場合、すべてのモジュールから検索する。</summary>
	/// <argument name="what">A combination of claim or view seperated by spaces.</argument>
	[Command(@"(?:find *dup(?:licate)?|dup(?:licate)? *find|search *dup(?:licate)?|dup(?:licate)? *search)( +.+)?")]
	public static void FindDuplicate([Group(1)] string queries, string user, bool isWhisper)
	{
		var allMatches = (string.IsNullOrEmpty(queries) ? TwitchGame.Instance.Modules : FindModules(queries.SplitFull(',', ';').Select(q => q.Trim()).Distinct().ToArray()))
			.GroupBy(module => module.HeaderText)
			.Where(grouping => grouping.Count() > 1)
			.Select(grouping => $"{grouping.Key} ({grouping.Select(module => module.Code).Join(", ")})");

		var modules = allMatches.Shuffle().Take(3).ToList();

		IRCConnection.SendMessage(modules.Count > 0 ? $"重複モジュールが{allMatches.Count()}個見つかりました: {modules.Join(", ")}" : "重複モジュールはありません。", user, !isWhisper);
	}

	/// <name>New Bomb</name>
	/// <syntax>newbomb</syntax>
	/// <summary>トレーニングモードで新しい爆弾を開始する。実行には一定ポイントの獲得または「処理担当者」以上の権限が必要である。</summary>
	[Command(@"newbomb")]
	public static void NewBomb(string user, bool isWhisper)
	{
		if (!OtherModes.TrainingModeOn)
		{
			IRCConnection.SendMessage($"@{user}さん：newbombコマンドはトレーニングモードでのみ有効です。", user, !isWhisper);
			return;
		}
		if (isWhisper)
		{
			IRCConnection.SendMessage($"@{user}さん：newbombコマンドはウィスパーコメントでは実行できません。", user, !isWhisper);
			return;
		}

		Leaderboard.Instance.GetRank(user, out var entry);
		if (entry == null || entry.SolveScore < TwitchPlaySettings.data.MinScoreForNewbomb && !UserAccess.HasAccess(user, AccessLevel.Defuser, true))
			IRCConnection.SendMessage($"@{user}さん：newbombコマンドをするための十分なポイントがありません。");
		else
		{
			TwitchPlaySettings.AddRewardBonus(-TwitchPlaySettings.GetRewardBonus());
			SolveBomb();
		}
	}

	/// <name>Fill Edgework</name>
	/// <syntax>filledgework</syntax>
	/// <summary>テキストベースでエッジワークを表示する。モデレーター権限以上かTwitchplaySettingsにて全員に許可する必要がある。</summary>
	[Command(@"filledgework")]
	public static void FillEdgework(string user, bool isWhisper)
	{
		if (!UserAccess.HasAccess(user, AccessLevel.Mod, true) && !TwitchPlaySettings.data.EnableFilledgeworkForEveryone && !TwitchPlaySettings.data.AnarchyMode)
			return;

		foreach (var bomb in TwitchGame.Instance.Bombs)
		{
			var str = bomb.FillEdgework();
			if (bomb.BombID == TwitchGame.Instance._currentBomb)
				IRCConnection.SendMessage(TwitchPlaySettings.data.BombEdgework, user, !isWhisper, str);
		}
	}

	/// <name>Elevator Edgework</name>
	/// <syntax>edgework (wall)</syntax>
	/// <summary>エレベーター上にあるエッジワークを表示する。(wall)は表示したい壁の面を指定する。例：right, left, back</summary>
	/// <restriction>ElevatorOnly</restriction>
	[Command(@"edgework((?: right| left| back| r| l| b)?)"), ElevatorOnly]
	public static IEnumerator EdgeworkElevator([Group(1)] string edge, string user, bool isWhisper) => Edgework(edge, user, isWhisper);
	/// <name>Edgework</name>
	/// <syntax>edgework (edge)\nedgework 45</syntax>
	/// <summary>エッジワークを見るために爆弾を回転させる。(edge)は表示したい壁の面を指定する。例：top, top left。45度回転させる場合は、45と入力する。</summary>
	/// <restriction>ElevatorDisallowed</restriction>
	[Command(@"edgework((?: 45|-45)|(?: top right| right top| right bottom| bottom right| bottom left| left bottom| left top| top left| left| top| right| bottom| tr| rt| tl| lt| br| rb| bl| lb| t| r| b| l))?"), ElevatorDisallowed]
	public static IEnumerator Edgework([Group(1)] string edge, string user, bool isWhisper)
	{
		if (TwitchPlaySettings.data.EnableEdgeworkCommand || TwitchPlaySettings.data.AnarchyMode)
			return TwitchGame.Instance.Bombs[TwitchGame.Instance._currentBomb == -1 ? 0 : TwitchGame.Instance._currentBomb].ShowEdgework(edge);
		else
		{
			string edgework = TwitchGame.Instance.Bombs.Count == 1 ?
				TwitchGame.Instance.Bombs[0].EdgeworkText.text :
				TwitchGame.Instance.Bombs.Select(bomb => $"{bomb.BombID} = {bomb.EdgeworkText.text}").Join(" //// ");

			IRCConnection.SendMessage(TwitchPlaySettings.data.BombEdgework, user, !isWhisper, edgework);
			return null;
		}
	}

	/// <name>Camera Wall</name>
	/// <syntax>camerawall [mode]</syntax>
	/// <summary>カメラウォールのモードをオン、オフ、自動のいずれかに設定する。自動カメラウォールが有効な場合、使用するにはMODランクが必要。</summary>
	/// <argument name="mode">The mode of the camera wall. Can be on, off or auto.</argument>
	[Command(@"(?:camerawall|cw) *(on|enabled?|off|disabled?|auto)")]
	public static void CameraWall(string user, [Group(1)] string mode)
	{
		if (TwitchPlaySettings.data.EnableAutomaticCameraWall && !UserAccess.HasAccess(user, AccessLevel.Mod, true))
		{
			IRCConnection.SendMessage("カメラの面は自動的に制御されます。モデレーターのみ変更できます。");
			return;
		}

		if (mode.EqualsAny("on", "enable", "enabled"))
			TwitchGame.ModuleCameras.CameraWallMode = ModuleCameras.Mode.Enabled;
		else if (mode.EqualsAny("off", "disable", "disabled"))
			TwitchGame.ModuleCameras.CameraWallMode = ModuleCameras.Mode.Disabled;
		else if (mode.StartsWith("auto"))
			TwitchGame.ModuleCameras.CameraWallMode = ModuleCameras.Mode.Automatic;
	}

	/// <name>Queue Named Command</name>
	/// <syntax>queue [name] [command]</syntax>
	/// <summary>名前付きのコマンドをキューに追加する。</summary>
	[Command(@"q(?:ueue)? +(?!\s*!)([^!]+) +(!.+)")]
	public static void EnqueueNamedCommand(IRCMessage msg, [Group(1)] string name, [Group(2)] string command)
	{
		if (name.Trim().EqualsIgnoreCase("all"))
		{
			IRCConnection.SendMessage(@"@{0}さん：キューに追加するコマンドの名前にallを利用することはできません。", msg.UserNickName, !msg.IsWhisper, msg.UserNickName);
			return;
		}
		TwitchGame.Instance.CommandQueue.Add(new CommandQueueItem(msg.Duplicate(command), name.Trim()));
		TwitchGame.ModuleCameras?.SetNotes();
		IRCConnection.SendMessage("@{0}さん：コマンドがキューされました。", msg.UserNickName, !msg.IsWhisper, msg.UserNickName);
		TwitchGame.Instance.CallUpdate(false);
	}

	/// <name>Queue Command</name>
	/// <syntax>queue [command]</syntax>
	/// <summary>順番に呼び出したいコマンドをキューに追加する。</summary>
	[Command(@"q(?:ueue)? +(!.+)")]
	public static void EnqueueUnnamedCommand(IRCMessage msg, [Group(1)] string command)
	{
		var simplifiedCommand = command.Trim().ToLowerInvariant();
		if (!UserAccess.HasAccess(msg.UserNickName, AccessLevel.Admin, true) && (simplifiedCommand.StartsWith("!q") || simplifiedCommand.StartsWith("!bomb")))
		{
			IRCConnection.SendMessage("@{0}さん：そのコマンドはキューに追加できません。", msg.UserNickName, !msg.IsWhisper, msg.UserNickName);
			return;
		}

		TwitchGame.Instance.CommandQueue.Add(new CommandQueueItem(msg.Duplicate(command)));
		TwitchGame.ModuleCameras?.SetNotes();
		IRCConnection.SendMessage("@{0}さん：コマンドがキューに追加されました。", msg.UserNickName, !msg.IsWhisper, msg.UserNickName);
		TwitchGame.Instance.CallUpdate(false);
	}

	/// <name>Unqueue/Show Command</name>
	/// <syntax>unqueue [command]\ndelqueue [command]\nshowqueue [command]</syntax>
	/// <summary>キューに入っているコマンドを削除、表示する。Unqueueは自分のコマンドだけを削除することができる。delqueueはモデレーターだけの操作であり、どんなコマンドでも削除することができる。</summary>
	/// <argument name="command">The command to find in the queue. Can be "all" for all of your commands or just all commands if delqueue is being used.</argument>
	[Command(@"(?:(un)|(del)|(show|list))q(?:ueue)?(?: *(all)| +(.+))?")]
	public static void UnqueueCommand(string user, bool isWhisper, [Group(1)] bool un, [Group(2)] bool del, [Group(3)] bool show, [Group(4)] bool all, [Group(5)] string command)
	{
		if (del && !UserAccess.HasAccess(user, AccessLevel.Mod, true))
		{
			IRCConnection.SendMessage($"@{user}さん：モデレーター権限が必要です。", user, !isWhisper);
			return;
		}
		if ((del || un) && !all && string.IsNullOrEmpty(command?.Trim()))
		{
			IRCConnection.SendMessage($"{user}さん：コマンド名を指定するか!{(del ? "del" : "un")}qallを利用してください。", user, !isWhisper);
			return;
		}
		var matchingItems = all && un
			? TwitchGame.Instance.CommandQueue.Where(item => item.Message.UserNickName == user).ToArray()
			: all || (show && string.IsNullOrEmpty(command?.Trim()))
				? TwitchGame.Instance.CommandQueue.Where(item => all || item.Message.UserNickName == user).ToArray()
				: command.StartsWith("!")
					? TwitchGame.Instance.CommandQueue.Where(item => (all || del || item.Message.UserNickName == user) && item.Message.Text.StartsWith(command + " ")).ToArray()
					: command.Trim().Length > 0
						? TwitchGame.Instance.CommandQueue.Where(item => (all || del || item.Message.UserNickName == user) && item.Name != null && item.Name == command.Trim()).ToArray()
						: TwitchGame.Instance.CommandQueue.Where(item => (all || del || item.Message.UserNickName == user) && item.Message.UserNickName.EqualsIgnoreCase(command)).ToArray();
		if (matchingItems.Length == 0)
		{
			IRCConnection.SendMessage(@"@{0}さん：そのようなコマンドはキューにありません。", user, !isWhisper, user);
			return;
		}
		if (!show)
		{
			IRCConnection.SendMessage($"@{user}さん：次のコマンドをキューから削除します: {matchingItems.Select(item => item.Message.Text + (item.Name != null ? $" ({item.Name})" : null)).Join("; ")}", user, !isWhisper);
			TwitchGame.Instance.CommandQueue.RemoveAll(item => matchingItems.Contains(item));
			TwitchGame.ModuleCameras?.SetNotes();
		} else
		{
			IRCConnection.SendMessage($"@{user}さん：次のコマンドがキュー内でマッチしました：{matchingItems.Select(item => item.Message.Text + (item.Name != null ? $" ({item.Name})" : null)).Join("; ")}", user, !isWhisper);

		}
	}

	/// <name>Queue On/Off</name>
	/// <syntax>queue on\nqueue off</syntax>
	/// <summary>キューをオン/オフにし、キューが必要かどうかを他の人に知らせる。</summary>
	[Command(@"q(?:ueue)?(on|off)")]
	public static void QueueEnabled([Group(1)] string state)
	{
		TwitchGame.Instance.QueueEnabled = state.EqualsIgnoreCase("on");
		TwitchGame.ModuleCameras?.SetNotes();
	}

	/// <name>Call Command</name>
	/// <syntax>call (name)\ncallnow (name)</syntax>
	/// <summary>キューに追加されたコマンドをコールする。callnow は、CallSet で設定された要件をスキップする。(name)が指定された場合は、キュー内の次のコマンドの代わりに、指定されたコマンドを呼び出す。</summary>
	[Command(@"call( *now)?( +.+)?")]
	public static void CallQueuedCommand(string user, [Group(1)] bool now, [Group(2)] string name)
	{
		name = (name?.Trim()) ?? "";
		var response = TwitchGame.Instance.CheckIfCall(false, now, user, name, out bool callChanged);
		if (response != TwitchGame.CallResponse.Success)
		{
			TwitchGame.Instance.SendCallResponse(user, name, response, callChanged);
			return;
		}
		if (callChanged) IRCConnection.SendMessageFormat("@{0}さん：コールが{1}に変更されました。", user, string.IsNullOrEmpty(name) ? "次のキューに" : name);
		TwitchGame.Instance.CommandQueue.Remove(TwitchGame.Instance.callSend);
		TwitchGame.ModuleCameras?.SetNotes();
		IRCConnection.SendMessageFormat("{0}{1}さんの「{2}」をコールします。", TwitchGame.Instance.callWaiting && string.IsNullOrEmpty(user)
			? "コール待機中です。"
			: now
				? "必要なコール数を回避し、"
				: TwitchGame.Instance.callsNeeded > 1
					? "必要なコール数に到達したため、"
					: "", TwitchGame.Instance.callSend.Message.UserNickName, TwitchGame.Instance.callSend.Message.Text);
		DeleteCallInformation(true);
		if (TwitchGame.Instance.Bombs.Any(x => x.BackdoorHandleHack))
			IRCConnection.SendMessage("ハッキングが検出されたため、ハッキングが終了するまでこのコマンドを実行できません。");
		TwitchGame.Instance.StartCoroutine(WaitForCall(new List<IRCMessage>(){ TwitchGame.Instance.callSend.Message }));
	}

	/// <name>Call All</name>
	/// <syntax>callall\ncallall force</syntax>
	/// <summary>無名のコマンドをすべてキューからコールする。forceをつけると、名前付きコマンドもコールされる。</summary>
	[Command(@"callall( *force)?")]
	public static void CallAllQueuedCommands(string user, bool isWhisper, [Group(1)] bool force)
	{
		if (TwitchGame.Instance.CommandQueue.Count == 0)
		{
			IRCConnection.SendMessage($"{user}さん：キューの中身は空です。", user, !isWhisper);
			return;
		}

		// Take a copy of the list in case executing one of the commands modifies the command queue
		var allCommands = TwitchGame.Instance.CommandQueue.Where(item => force || item.Name == null).ToList();
		if (allCommands.Count == 0)
		{
			IRCConnection.SendMessage($"{user}さん：キューには名前付きのコマンドのみが含まれています。すべてコールするには、「!callall force」を使用してください。", user, !isWhisper);
			return;
		}

		TwitchGame.Instance.CommandQueue.RemoveAll(item => allCommands.Contains(item));
		TwitchGame.ModuleCameras?.SetNotes();
		List<IRCMessage> cmdsToExecute = new List<IRCMessage>();
		foreach (var call in allCommands)
		{
			IRCConnection.SendMessageFormat("{0}さんのコマンド「{1}」をコールしています。", call.Message.UserNickName, call.Message.Text);
			cmdsToExecute.Add(call.Message);
		}
		DeleteCallInformation(true);
		if (TwitchGame.Instance.Bombs.Any(x => x.BackdoorHandleHack))
			IRCConnection.SendMessageFormat("ハッキングが検出されました。ハッキング終了後、{0}コマンドは実行されます。", cmdsToExecute.Count == 1 ? "この" : "これらの");
		TwitchGame.Instance.StartCoroutine(WaitForCall(cmdsToExecute));
	}

	/// <name>Call Set</name>
	/// <syntax>callset [minimum]</syntax>
	/// <summary>コマンドのコールに必要な!callの最小回数を設定する。</summary>
	[Command(@"callset +(\d*)")]
	public static void CallSetCommand(string user, [Group(1)] int minimum)
	{
		if (minimum <= 0 || minimum >= 25)
		{
			IRCConnection.SendMessageFormat("@{0}さん：{1}はコール数として不適切な値です。", user, minimum);
			return;
		}

		TwitchGame.Instance.callsNeeded = minimum;
		DeleteCallInformation(true);
		IRCConnection.SendMessageFormat("コール数の最小回数が{0}に設定されました。", minimum);
	}

	/// <name>Call Count</name>
	/// <syntax>callcount</syntax>
	/// <summary>最後にコールされたコマンドから数えて、!Callが実行された回数を表示する。</summary>
	[Command(@"callcount")]
	public static void CallCountCommand() => IRCConnection.SendMessageFormat("{0}/{1}名からのコールが行われました。", TwitchGame.Instance.CallingPlayers.Count, TwitchGame.Instance.callsNeeded);

	/// <name>Delete Call</name>
	/// <syntax>delcall [user]</syntax>
	/// <summary>ユーザーのコールを取り消す。</summary>
	/// <restriction>Mod</restriction>
	[Command(@"delcall +(.+)", AccessLevel.Mod, AccessLevel.Mod)]
	public static void DeleteQueuedPlayer([Group(1)] string callUser, string user)
	{
		callUser = callUser.FormatUsername();
		if (string.IsNullOrEmpty(callUser)) IRCConnection.SendMessageFormat("@{0}さん：除去したいコールをしたユーザー名を指定してください！", user);
		else if (!TwitchGame.Instance.CallingPlayers.Keys.Contains(user)) IRCConnection.SendMessageFormat("@{0}さん@{1}さんはコールしていません。", user, callUser);
		else
		{
			TwitchGame.Instance.CallingPlayers.Remove(callUser);
			IRCConnection.SendMessageFormat("@{0}さん：@{1}さんのコールを削除しました。", user, callUser);
			TwitchGame.Instance.CallUpdate(true);
		}
	}

	/// <name>Uncall</name>
	/// <syntax>uncall</syntax>
	/// <summary>複数のコールコマンドが必要な際に、コールコマンドを取り消す。</summary>
	[Command(@"uncall")]
	public static void RemoveCalledPlayer(string user)
	{
		if (!TwitchGame.Instance.CallingPlayers.Keys.Contains(user))
		{
			IRCConnection.SendMessageFormat("@{0}さん：あなたはコールしていません。", user);
			return;
		}
		TwitchGame.Instance.CallingPlayers.Remove(user);
		IRCConnection.SendMessageFormat("@{0}さん：あなたのコールは削除されました。", user);
		TwitchGame.Instance.CallUpdate(true);
	}

	/// <name>Call Players</name>
	/// <syntax>callplayers</syntax>
	/// <summary>複数のコールが必要な際、現在コールしているプレイヤーをリストアップする。</summary>
	[Command(@"callplayers")]
	public static void ListCalledPlayers()
	{
		int totalCalls = TwitchGame.Instance.CallingPlayers.Count;
		if (totalCalls == 0)
		{
			IRCConnection.SendMessageFormat("コールはありません。");
			return;
		}
		string[] __calls = TwitchGame.Instance.CallingPlayers.Values.ToArray();
		string[] __callPlayers = TwitchGame.Instance.CallingPlayers.Keys.ToArray();
		string builder = "";
		for (int j = 0; j < __calls.Length; j++) builder = builder + ((j == 0) ? "@" : ", @") + __callPlayers[j] + ": " + (string.IsNullOrEmpty(__calls[j]) ? "キュー内の次のコマンド：" : __calls[j]);
		IRCConnection.SendMessageFormat("以下のプレイヤーがコールを行いました：{0}", builder);
	}

	/// <name>Delete All Calls</name>
	/// <syntax>delcallall</syntax>
	/// <summary>すべてのコールを削除する。</summary>
	/// <restriction>Mod</restriction>
	[Command(@"delcallall", AccessLevel.Mod, AccessLevel.Mod)]
	public static void DeleteCallInformation(bool silent)
	{
		TwitchGame.Instance.CallingPlayers.Clear();
		TwitchGame.Instance.callWaiting = false;
		if (!silent) IRCConnection.SendMessageFormat("すべてのコール情報が削除されました。");
	}

	/// <name>Set Multiplier</name>
	/// <syntax>setmultiplier [multiplier]</syntax>
	/// <summary>倍率を設定する。</summary>
	/// <restriction>Admin</restriction>
	[Command(@"setmultiplier +(\d*\.?\d+)", AccessLevel.Admin, AccessLevel.Admin)]
	public static void SetMultiplier([Group(1)] float multiplier) => OtherModes.SetMultiplier(multiplier);

	/// <name>Solve Bomb</name>
	/// <syntax>solvebomb</syntax>
	/// <summary>現在持っている爆弾を解除する。</summary>
	/// <restriction>SuperUser</restriction>
	[Command(@"solvebomb", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static void SolveBomb()
	{
		foreach (var bomb in TwitchGame.Instance.Bombs.Where(x => GameRoom.Instance.IsCurrentBomb(x.BombID)))
			bomb.StartCoroutine(bomb.KeepAlive());

		var modules = TwitchGame.Instance.Modules
			.Where(x => GameRoom.Instance.IsCurrentBomb(x.BombID))
			.OrderByDescending(module => module.Solver.ModInfo.moduleID.EqualsAny("cookieJars", "organizationModule", "forgetMeLater", "encryptedHangman", "SecurityCouncil", "GSAccessCodes", "Kuro"));
		foreach (var module in modules)
			if (!module.Solved)
				module.SolveSilently();
	}

	/// <name>Enable Claims</name>
	/// <syntax>enableclaims</syntax>
	/// <summary>モジュールの割り当てを有効にする。</summary>
	/// <restriction>Admin</restriction>
	[Command(@"enableclaims", AccessLevel.Admin, AccessLevel.Admin)]
	public static void EnableClaims()
	{
		TwitchModule.ClaimsEnabled = true;
		IRCConnection.SendMessage("モジュールの割り当ては有効になりました。");
	}

	/// <name>Disable Claims</name>
	/// <syntax>disableclaims</syntax>
	/// <summary>モジュールの割り当てを無効にする。</summary>
	/// <restriction>Admin</restriction>
	[Command(@"disableclaims", AccessLevel.Admin, AccessLevel.Admin)]
	public static void DisableClaims()
	{
		TwitchModule.ClaimsEnabled = false;
		IRCConnection.SendMessage("モジュールの割り当ては無効になりました。");
	}

	/// <name>Assign</name>
	/// <syntax>assign [user] [codes]</syntax>
	/// <summary>モジュール番号を指定してユーザーにモジュールを振り分ける。</summary>
	[Command(@"assign +(\S+) +(.+)")]
	public static void AssignModuleTo([Group(1)] string targetUser, [Group(2)] string queries, string user)
	{
		targetUser = targetUser.FormatUsername();
		if (TwitchPlaySettings.data.AnarchyMode)
		{
			IRCConnection.SendMessage($"{user}さん：アナーキーモード中は、モジュールの振り分けは許可されていません。");
			return;
		}

		var query = queries.SplitFull(' ', ',', ';');
		var denied = new List<string>();
		foreach (var module in TwitchGame.Instance.Modules.Where(m => !m.Solved && GameRoom.Instance.IsCurrentBomb(m.BombID) && query.Any(q => q.EqualsIgnoreCase(m.Code))).Take(TwitchPlaySettings.data.ModuleClaimLimit))
		{
			if ((module.PlayerName != user || module.ClaimQueue.All(q => q.UserNickname == targetUser)) && !UserAccess.HasAccess(user, AccessLevel.Mod, true))
				denied.Add(module.Code);
			else
				ModuleCommands.Assign(module, user, targetUser);
		}
		if (denied.Count == 1)
			IRCConnection.SendMessage($"{user}さん：あなたはモデレーターではないため、{denied[0]}は再度振り分けできません。", user, false);
		else if (denied.Count > 1)
			IRCConnection.SendMessage($"{user}さん：あなたはモデレーターではないため、{denied.Take(denied.Count - 1).Join(", ")}, {denied.Last()} は再度振り分けできません。", user, false);
	}

	/// <name>Bot Unclaim</name>
	/// <syntax>bot unclaim</syntax>
	/// <summary>ボットに割り当てられたモジュールの割り当てを解除する。</summary>
	/// <restriction>Mod</restriction>
	[Command(@"bot ?unclaim( ?all)?", AccessLevel.Mod, AccessLevel.Mod)]
	public static void BotUnclaim()
	{
		foreach (var module in TwitchGame.Instance.Modules)
			if (!module.Solved && module.PlayerName == IRCConnection.Instance.UserNickName && GameRoom.Instance.IsCurrentBomb(module.BombID))
				module.SetUnclaimed();
	}

	/// <name>Disable Interactive</name>
	/// <syntax>disableinteractive</syntax>
	/// <summary>カメラのインタラクティブ・モードを無効にする。Escキーが押されなかったときのように画面が固定される。</summary>
	/// <restriction>Mod</restriction>
	[Command(@"disableinteractive", AccessLevel.Mod, AccessLevel.Mod)]
	public static void DisableInteractive() => TwitchGame.ModuleCameras.DisableInteractive();

	/// <name>Return To Setup</name>
	/// <syntax>returntosetup</syntax>
	/// <summary>事務所に戻る。</summary>
	/// <restriction>Mod</restriction>
	[Command(@"(?:returntosetup|leave|exit)(?:room)?|return", AccessLevel.Mod, AccessLevel.Mod)]
	public static void ReturnToSetup() => SceneManager.Instance.ReturnToSetupState();

	/// <name>Enable Interactive Mode</name>
	/// <syntax>enableinteractivemode</syntax>
	/// <summary>インタラクティブ・モードを有効にする。「Streamer」権限を持つ人のみ可能。</summary>
	/// <restriction>Streamer</restriction>
	[Command(@"enableinteractivemode", AccessLevel.Streamer, AccessLevel.Streamer)]
	public static void EnableInteractiveMode()
	{
		IRCConnection.SendMessage("インタラクティブモードが有効になりました。");
		TwitchPlaySettings.data.EnableInteractiveMode = true;
		TwitchPlaySettings.WriteDataToFile();
		TwitchGame.EnableDisableInput();
	}

	/// <name>Disable Interactive Mode</name>
	/// <syntax>disableinteractivemode</syntax>
	/// <summary>インタラクティブモードを無効にし、配信者が爆弾を操作できないようにする。</summary>
	/// <restriction>Streamer</restriction>
	[Command(@"disableinteractivemode", AccessLevel.Streamer, AccessLevel.Streamer)]
	public static void DisableInteractiveMode()
	{
		IRCConnection.SendMessage("インタラクティブモードは無効になりました。");
		TwitchPlaySettings.data.EnableInteractiveMode = false;
		TwitchPlaySettings.WriteDataToFile();
		TwitchGame.EnableDisableInput();
	}

	/// <name>Solve Unsupported Modules</name>
	/// <syntax>solveunsupportedmodules</syntax>
	/// <summary>TP未対応のモジュールを解除する。</summary>
	/// <restriction>Admin</restriction>
	[Command(@"solveunsupportedmodules", AccessLevel.Admin, AccessLevel.Admin)]
	public static void SolveUnsupportedModules()
	{
		IRCConnection.SendMessage("非対応のモジュールを解除します。");
		TwitchModule.SolveUnsupportedModules();
	}

	/// <name>Solve Boss Modules</name>
	/// <syntax>solvebossmodules</syntax>
	/// <summary>爆弾の解除数に依存するモジュールや、マニュアルページのリポジトリ上ボスモジュールとみなされるモジュールを解除する。</summary>
	/// <restriction>SuperUser</restriction>
	[Command(@"solvebossmodules", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static void SolveBossModules()
	{
		IRCConnection.SendMessage("ボスモジュールを解除します。");
		TwitchGame.Instance.SolveBossModules();
	}

	/// <name>Custom Messages</name>
	/// <syntax>ttks\nttksleft\nttksright\ninfozen\nqhelp</syntax>
	/// <summary>あらかじめ定義されたメッセージをチャットに送信します。配信者はTwitchPlaySettings.jsonにてメッセージをカスタマイズできるが、ここで紹介したものはデフォルトで含まれている。</summary>
	[Command(null)]
	public static bool DefaultCommand(string cmd, string user, bool isWhisper)
	{
		if (TwitchPlaySettings.data.BombCustomMessages.ContainsKey(cmd.ToLowerInvariant()))
		{
			IRCConnection.SendMessage(TwitchPlaySettings.data.BombCustomMessages[cmd.ToLowerInvariant()], user, !isWhisper);
			return true;
		}
		return false;
	}

	/// <name>Show Mission Name</name>
	/// <syntax>status\nmission</syntax>
	/// <summary>現在進行中のミッションを表示する。</summary>
	[Command(@"(?:status|mission)")]
	public static void Mission(string cmd, string user, bool isWhisper)
	{
		if (GameplayState.MissionToLoad.EqualsAny(Assets.Scripts.Missions.FreeplayMissionGenerator.FREEPLAY_MISSION_ID, ModMission.CUSTOM_MISSION_ID))
		{
			IRCConnection.SendMessage(TwitchPlaySettings.data.CurrentMissionNull, user, !isWhisper);
			return;
		}
		var missionTerm = SceneManager.Instance.GameplayState.Mission.DisplayNameTerm;
		string missionName = Localization.GetLocalizedString(missionTerm);
		string missionLink = UrlHelper.MissionLink(missionName);
		IRCConnection.SendMessage(TwitchPlaySettings.data.CurrentMission, user, !isWhisper, missionName, missionLink);
	}

	#endregion

	#region Private methods
	private static void ClaimViewPin(string user, bool isWhisper, IEnumerable<TwitchModule> modules, bool claim = false, bool view = false, bool pin = false)
	{
		if (isWhisper)
		{
			IRCConnection.SendMessage($"{user}さん：ウィスパーコメントでは、モジュールの割り当ては行えません。", user, false);
			return;
		}
		if (TwitchPlaySettings.data.AnarchyMode)
		{
			IRCConnection.SendMessage($"{user}さん：アナーキーモードでは、モジュールの割り当ては行えません。");
			return;
		}
		foreach (var module in modules)
		{
			if (claim)
				module.AddToClaimQueue(user, view, pin);
			else if (view)
				module.ViewPin(user, pin);
		}
	}

	private static IEnumerable<TwitchModule> FindModules(string[] queries, Func<TwitchModule, bool> predicate = null) => TwitchGame.Instance.Modules
			.Where(module => queries.Any(q => module.HeaderText.ContainsIgnoreCase(q)) && GameRoom.Instance.IsCurrentBomb(module.BombID) && (predicate == null || predicate(module)) && !module.Hidden)
			.OrderByDescending(handle => queries.Any(q => handle.HeaderText.EqualsIgnoreCase(q)))
			.ThenBy(handle => handle.Solved)
			.ThenBy(handle => handle.PlayerName != null);

	private static void ShowClaimsOfUser(string targetUser, bool isWhisper, string ownedListMsg, string noOwnedMsg)
	{
		targetUser = targetUser.FormatUsername();
		var claimed = TwitchGame.Instance.Modules
			.Where(module => module.PlayerName != null && module.PlayerName.EqualsIgnoreCase(targetUser) && !module.Solved)
			.Select(module => string.Format(TwitchPlaySettings.data.OwnedModule, module.Code, module.HeaderText))
			.Shuffle()
			.ToList();
		if (claimed.Count > 0)
		{
			string newMessage = string.Format(ownedListMsg, targetUser, string.Join(", ", claimed.ToArray(), 0, Math.Min(claimed.Count, 5)));
			if (claimed.Count > 5)
				newMessage += $", その他{claimed.Count - 5}個";
			IRCConnection.SendMessage(newMessage, targetUser, !isWhisper);
		}
		else
			IRCConnection.SendMessage(string.Format(noOwnedMsg, targetUser), targetUser, !isWhisper);
	}

	// Makes sure that all called commands are not executed until you are done being hacked by Backdoor Hacking
	private static IEnumerator WaitForCall(List<IRCMessage> cmdsToExecute)
	{
		bool alreadyExecuting = calledCommands.Count != 0;
		calledCommands.AddRange(cmdsToExecute);
		if (alreadyExecuting) yield break;

		if (TwitchGame.Instance.Bombs.Any(x => x.BackdoorHandleHack)) {
			yield return new WaitUntil(() => TwitchGame.Instance.Bombs.All(x => !x.BackdoorHandleHack));
			IRCConnection.SendMessage("ハッキングが終了しました。ハッキング中に入力されたすべてのコマンドを実行します。");
		}
		foreach (IRCMessage m in calledCommands)
			IRCConnection.ReceiveMessage(m);
		calledCommands.Clear();
	}
	#endregion
}
