using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>爆弾起動中に使えるコマンド</summary>
static class ModuleCommands
{
	/// <name>Help</name>
	/// <syntax>help</syntax>
	/// <summary>モジュール解除に使用できるコマンドを表示する。</summary>
	/// <restriction>SolvedAllowed</restriction>
	[Command(@"(?:help|manual)( +pdf)?"), SolvedAllowed]
	public static void Help(TwitchModule module, [Group(1)] bool pdf)
	{
		string manualType = pdf ? "pdf" : "html";

		string manualText = module.Solver.ManualCode;
		string helpText = string.IsNullOrEmpty(module.Solver.HelpMessage) ? string.Empty : string.Format(module.Solver.HelpMessage, module.Code, module.HeaderText);
		if (module.TranslatedName != module.HeaderText && module.TranslatedAuthor != "") { module.TranslatedAuthor = "(" + module.TranslatedAuthor + ")"; };
		IRCConnection.SendMessage($"{module.TranslatedName}({module.HeaderText}) : {helpText} : {UrlHelper.ManualFor(module.HeaderText, module.TranslatedName,module.TranslatedAuthor, manualType, VanillaRuleModifier.GetModuleRuleSeed(module.Solver.ModInfo.moduleID) != 1)}");
	}

	/// <name>Player</name>
	/// <syntax>player</syntax>
	/// <summary>どのユーザーがそのモジュールに割り当てられているかを表示する。</summary>
	/// <restriction>SolvedAllowed</restriction>
	[Command("player"), SolvedAllowed]
	public static void Player(TwitchModule module, string user) => IRCConnection.SendMessage(module.PlayerName != null
			? string.Format(TwitchPlaySettings.data.ModulePlayer, module.Code, module.PlayerName, module.Solver.ModInfo.moduleTranslatedName ?? module.HeaderText)
			: string.Format(TwitchPlaySettings.data.ModuleNotClaimed, user, module.Code, module.Solver.ModInfo.moduleTranslatedName ?? module.HeaderText));

	/// <name>Queue Flip</name>
	/// <syntax>queue flip</syntax>
	/// <summary>モジュールが解除された後に爆弾を裏返すようにキューに追加する。</summary>
	[Command("(?:bomb|queue) +(?:turn(?: +a?round)?|flip|spin)")]
	public static void BombTurnAround(TwitchModule module)
	{
		if (!module.Solver.TurnQueued)
		{
			module.Solver.TurnQueued = true;
			module.StartCoroutine(module.Solver.TurnBombOnSolve());
		}
		IRCConnection.SendMessageFormat(TwitchPlaySettings.data.TurnBombOnSolve, module.Code, module.Solver.ModInfo.moduleTranslatedName ?? module.HeaderText);
	}

	/// <name>Cancel Queued Flip</name>
	/// <syntax>cancel queue flip</syntax>
	/// <summary>モジュールが解除された後に行われる直前の回転をキューから取り消す。</summary>
	[Command("cancel +(?:bomb|queue) +(?:turn(?: +a?round)?|flip|spin)")]
	public static void BombTurnAroundCancel(TwitchModule module)
	{
		module.Solver.TurnQueued = false;
		IRCConnection.SendMessageFormat(TwitchPlaySettings.data.CancelBombTurn, module.Code, module.Solver.ModInfo.moduleTranslatedName ?? module.HeaderText);
	}

	/// <name>Claim</name>
	/// <syntax>claim</syntax>
	/// <summary>モジュールの割り当てや、割り当て済みのモジュールに対して割り当てのキューを行う。</summary>
	[Command("claim")]
	public static void Claim(TwitchModule module, string user, bool isWhisper) => ClaimViewOrPin(module, user, isWhisper, view: false, pin: false);

	/// <name>Unview</name>
	/// <syntax>unview</syntax>
	/// <summary>カメラによるモジュールの表示を停止する。</summary>
	[Command("unview")]
	public static void Unview(TwitchModule module) => TwitchGame.ModuleCameras?.UnviewModule(module);

	/// <name>View / ViewPin</name>
	/// <syntax>view\nviewpin</syntax>
	/// <summary>専用画面でモジュールを表示する。viewpinはモデレーター以上であるか、いつでもピン可能なモジュールである必要である。</summary>
	[Command("(view(?: *pin)?|pin *view)")]
	public static void View(TwitchModule module, string user, [Group(1)] string cmd) => module.ViewPin(user, cmd.ContainsIgnoreCase("p"));

	public static IEnumerator Show(TwitchModule module, object yield)
	{
		bool select = !module.BombComponent.GetModuleID().EqualsAny("lookLookAway");
		IEnumerator focusCoroutine = module.Bomb.Focus(module.Selectable, module.FocusDistance, module.FrontFace, select);
		while (focusCoroutine.MoveNext())
			yield return focusCoroutine.Current;

		yield return new WaitForSeconds(0.5f);
		yield return yield is float delay ? new WaitForSecondsWithCancel(delay, false, module.Solver) : yield;
		if (CoroutineCanceller.ShouldCancel)
		{
			module.StartCoroutine(module.Bomb.Defocus(module.Selectable, module.FrontFace, select));
			yield break;
		}
		IEnumerator defocusCoroutine = module.Bomb.Defocus(module.Selectable, module.FrontFace, select);
		while (defocusCoroutine.MoveNext())
			yield return defocusCoroutine.Current;

		yield return new WaitForSeconds(0.5f);
	}

	/// <name>Solve</name>
	/// <syntax>solve</syntax>
	/// <summary>モジュールを強制的に解除する。「Admin」権限またはTP解除不可モジュールである必要がある。</summary>
	[Command("solve")]
	public static void Solve(TwitchModule module, string user)
	{
		if (
			// Admins can always auto-solve a module.
			UserAccess.HasAccess(user, AccessLevel.Admin, true) ||
			// Unsupported modules can always be auto-solved
			(module.Unsupported || module.Solver.GetType() == typeof(UnsupportedModComponentSolver))
		)
			module.Solver.SolveModule($"モジュール ({module.Solver.ModInfo.moduleTranslatedName ?? module.HeaderText})は自動的に解除されます。");
	}

	/// <name>Votesolve</name>
	/// <syntax>votesolve</syntax>
	/// <summary>モジュールの解除に関する投票を開始する。</summary>
	[Command("votesolve")]
	public static void VoteSolve(TwitchModule module, string user) => Votes.StartVote(user, VoteTypes.Solve, module);

	/// <name>Claim View Pin</name>
	/// <syntax>claim view pin\ncvp</syntax>
	/// <summary>モジュールを割り当て＆表示＆ピン留めする。3つのアクションのうちいずれかを取り除いてもいい。(claim view = cvなど)</summary>
	[Command("solve")]
	[Command(@"(claim view|view claim|claimview|viewclaim|cv|vc|claim view pin|view pin claim|claimviewpin|viewpinclaim|cvp|vpc)")]
	public static void ClaimViewPin(TwitchModule module, string user, bool isWhisper, [Group(1)] string cmd) => ClaimViewOrPin(module, user, isWhisper, view: true, pin: cmd.Contains("p"));

	/// <name>Unclaim</name>
	/// <syntax>unclaim\nunclaim unview</syntax>
	/// <summary>モジュールの割り当てや予約済み割り当てを解除する。unclaim unviewでは、モジュールの非表示も行われる。</summary>
	[Command("(unclaim|un?c|unclaim unview|unview unclaim|unclaimview|unviewclaim|uncv|unvc)")]
	public static void Unclaim(TwitchModule module, string user, [Group(1)] string cmd)
	{
		if (module.Solved)
		{
			IRCConnection.SendMessageFormat(TwitchPlaySettings.data.AlreadySolved, module.Code, module.PlayerName, user, module.Solver.ModInfo.moduleTranslatedName ?? module.HeaderText);
			return;
		}

		// If module is already unclaimed, just remove from claim queue
		if (module.PlayerName == null)
		{
			module.RemoveFromClaimQueue(user);
			return;
		}

		// Error if a non-mod tries to unclaim someone else’s module
		if (!UserAccess.HasAccess(user, AccessLevel.Mod, true) && module.PlayerName != user)
		{
			IRCConnection.SendMessage($"{user}さん：モジュール「{module.Code}」({module.Solver.ModInfo.moduleTranslatedName ?? module.HeaderText})はあなたに割り当てられていません。");
			return;
		}

		module.SetUnclaimed();
		if (cmd.Contains("v"))
			TwitchGame.ModuleCameras?.UnviewModule(module);
	}

	/// <name>Solved</name>
	/// <syntax>solved</syntax>
	/// <summary>モジュールのIDタグの色を緑に変更し、「解除済み」であることを示す。</summary>
	/// <restriction>Mod</restriction>
	[Command(@"solved", AccessLevel.Mod, AccessLevel.Mod)]
	public static void Solved(TwitchModule module, string user)
	{
		module.SetBannerColor(module.SolvedBackgroundColor);
		module.PlayerName = null;
		IRCConnection.SendMessageFormat(TwitchPlaySettings.data.ModuleReady, module.Code, user, module.Solver.ModInfo.moduleTranslatedName ?? module.HeaderText);
	}

	/// <name>Assign</name>
	/// <syntax>assign [username]</syntax>
	/// <summary>モジュールを他のユーザーに割り当てる。通常は「Mod」権限が必要だが、そのモジュールが割り当てられている場合、他のユーザーに割り当てることができる。</summary>
	[Command(@"assign +(.+)")]
	public static void Assign(TwitchModule module, string user, [Group(1)] string targetUser)
	{
		targetUser = targetUser.FormatUsername();
		if (module.PlayerName == targetUser)
		{
			IRCConnection.SendMessage($"{user}さん：このモジュールはすでに{targetUser}さんに割り当てられています。");
			return;
		}

		if (TwitchPlaySettings.data.AnarchyMode)
		{
			IRCConnection.SendMessage($"{user}さん：アナーキーモードでは、モジュールの振り分けは許可されていません。");
			return;
		}

		if (!UserAccess.HasAccess(user, AccessLevel.Mod, true))
		{
			if (module.PlayerName != user || !module.ClaimQueue.Any(q => q.UserNickname == targetUser))
			{
				IRCConnection.SendMessage($"{user}さん：{module.Code}は、自身に割り当てられている、かつ別の人が予約済みの割り当てを行っている場合にのみ振り分けることができます。");
				return;
			}
			if (TwitchGame.Instance.Modules.Count(md => !md.Solved && targetUser.EqualsIgnoreCase(md.PlayerName)) >= TwitchPlaySettings.data.ModuleClaimLimit)
			{
				IRCConnection.SendMessage($"{user}さん：他のユーザーの割り当て上限を超えてしまうため、{module.Code}は振り分けることができません。");
				return;
			}
		}

		if (module.TakeInProgress != null)
		{
			module.StopCoroutine(module.TakeInProgress);
			module.TakeInProgress = null;
			module.TakeUser = null;
		}

		module.SetClaimedBy(targetUser);
		IRCConnection.SendMessageFormat(TwitchPlaySettings.data.AssignModule, module.Code, module.PlayerName, user, module.HeaderText);
	}

	/// <name>Take</name>
	/// <syntax>take</syntax>
	/// <summary>モジュールを割り当てられている人に割り当てを解除するように要求する。</summary>
	[Command(@"take")]
	public static void Take(TwitchModule module, string user, bool isWhisper)
	{
		if (isWhisper)
			IRCConnection.SendMessage($"@{user}さん：ウィスパーコメントでは、モジュールの奪取は行えません。");
		else if (TwitchPlaySettings.data.AnarchyMode)
			IRCConnection.SendMessage($"@{user}さん：アナーキーモードでは、モジュールの奪取は行えません。");

		// Module is already claimed by the same user
		else if (module.PlayerName == user)
			IRCConnection.SendMessageFormat(TwitchPlaySettings.data.ModuleAlreadyOwned, user, module.Code, module.Solver.ModInfo.moduleTranslatedName ?? module.HeaderText);

		// Module is not claimed at all: just claim it
		else if (module.PlayerName == null)
			IRCConnection.SendMessage(module.TryClaim(user).Message);

		// If there's already a queued command for the module, it could be problematic to take it.
		// However there still may be reasons to take it anyway, so ask for confirmation.
		else if (!module.TakeConfirmationShown && TwitchGame.Instance.CommandQueue.Any(c => c.Message.Text.StartsWith($"!{module.Code} ")))
		{
			IRCConnection.SendMessage($"@{user}さん：そのモジュールには、キューに入れられているコマンドがあります。それでも奪取したい場合は、再度「!{module.Code} take」を入力してください。");
			module.TakeConfirmationShown = true;
		}

		// Attempt to take over from another user
		else
		{
			module.TakeConfirmationShown = false;

			module.AddToClaimQueue(user);
			if (module.TakeInProgress != null)
				IRCConnection.SendMessageFormat(TwitchPlaySettings.data.TakeInProgress, user, module.Code, module.Solver.ModInfo.moduleTranslatedName ?? module.HeaderText);
			else
			{
				IRCConnection.SendMessageFormat(TwitchPlaySettings.data.TakeModule, module.PlayerName, user, module.Code, module.Solver.ModInfo.moduleTranslatedName ?? module.HeaderText);
				module.TakeUser = user;
				module.TakeInProgress = module.StartCoroutine(module.ProcessTakeover());
			}
		}
	}

	/// <name>Mine</name>
	/// <syntax>mine</syntax>
	/// <summary>まだそのモジュールを担当していることを知らせる。割り当てをしている本人のみ、takeコマンドの内容を取り消すことができる。</summary>
	[Command(@"mine")]
	public static void Mine(TwitchModule module, string user, bool isWhisper)
	{
		if (isWhisper)
		{
			IRCConnection.SendMessage($"@{user}さん：ウィスパーモードでは、モジュールの奪取は行えません。", user, false);
			return;
		}

		// The module belongs to this user and there’s a takeover attempt in progress: cancel the takeover attempt
		if (module.PlayerName == user && module.TakeInProgress != null)
		{
			IRCConnection.SendMessageFormat(TwitchPlaySettings.data.ModuleIsMine, module.PlayerName, module.Code, module.Solver.ModInfo.moduleTranslatedName ?? module.HeaderText);
			module.StopCoroutine(module.TakeInProgress);
			module.TakeInProgress = null;
			module.TakeUser = null;
			module.TakeConfirmationShown = false;
		}

		// The module isn’t claimed: just claim it
		else if (module.PlayerName == null)
			IRCConnection.SendMessage(module.TryClaim(user).Message);

		// Someone else has a claim on the module
		else if (module.PlayerName != user)
			IRCConnection.SendMessageFormat(TwitchPlaySettings.data.AlreadyClaimed, module.Code, module.PlayerName, user, module.Solver.ModInfo.moduleTranslatedName ?? module.HeaderText);

		// If the user has a claim on the module but there’s no takeover attempt, just ignore this command
	}

	/// <name>Cancel Take</name>
	/// <syntax>canceltake</syntax>
	/// <summary>takeコマンドを取り消す。割り当てを行ったプレイヤーもしくは「Mod」権限以上のプレイヤーのみ行える。</summary>
	[Command(@"canceltake")]
	public static void CancelTake(TwitchModule module, string user, bool isWhisper)
	{
		if (module.TakeInProgress == null)
		{
			IRCConnection.SendMessage($"@{user}さん：{module.Code}({module.HeaderText})の奪取に対する試みは、現在行われていません。", user, !isWhisper);
			return;
		}

		if (!UserAccess.HasAccess(user, AccessLevel.Mod, true) && module.TakeUser != user)
		{
			IRCConnection.SendMessage($"@{user}さん：モデレーターを除き、モジュールの奪取をキャンセルできるのは奪取する本人のみです。");
			return;
		}

		// Cancel the takeover attempt
		IRCConnection.SendMessage($"{module.TakeUser}による{module.Code} ({module.HeaderText})の奪取は{user}により、キャンセルされました。");
		module.StopCoroutine(module.TakeInProgress);
		module.TakeInProgress = null;
		module.TakeUser = null;
		module.TakeConfirmationShown = false;
	}

	/// <name>Points</name>
	/// <syntax>points</syntax>
	/// <summary>モジュールのポイントを確認する。</summary>
	/// <restrictions>SolvedAllowed</restrictions>
	[Command(@"(points|score)"), SolvedAllowed]
	public static void Points(TwitchModule module) => IRCConnection.SendMessage($"{module.HeaderText}({module.Code})のスコア：{module.Solver.ModInfo.ScoreExplanation}");

	/// <name>Mark</name>
	/// <syntax>mark</syntax>
	/// <summary>モジュールのIDタグの色を黒に変更する。</summary>
	/// <restriction>Mod</restriction>
	[Command(@"mark", AccessLevel.Mod, AccessLevel.Mod)]
	public static void Mark(TwitchModule module) => module.SetBannerColor(module.MarkedBackgroundColor);

	/// <name>Unmark</name>
	/// <syntax>unmark</syntax>
	/// <summary>モジュールのIDタグの色をもとに戻す。</summary>
	/// <restriction>Mod</restriction>
	[Command(@"unmark", AccessLevel.Mod, AccessLevel.Mod)]
	public static void Unmark(TwitchModule module) => module.SetBannerColor(module.Claimed ? module.ClaimedBackgroundColour : module.unclaimedBackgroundColor);

	public static IEnumerator Zoom(TwitchModule module, SuperZoomData zoomData, object yield)
	{
		module.HideBanner();

		var zoomCoroutine = TwitchGame.ModuleCameras?.ZoomCamera(module, zoomData, 1);
		if (zoomCoroutine != null)
			while (zoomCoroutine.MoveNext())
				yield return zoomCoroutine.Current;

		yield return yield is float delay ? new WaitForSecondsWithCancel(delay, false, module.Solver) : yield;

		if (CoroutineCanceller.ShouldCancel)
		{
			module.ShowBanner(0.0f);
			module.StartCoroutine(TwitchGame.ModuleCameras?.UnzoomCamera(module, zoomData, 0));
			yield break;
		}

		module.ShowBanner();
		var unzoomCoroutine = TwitchGame.ModuleCameras?.UnzoomCamera(module, zoomData, 1);
		if (unzoomCoroutine != null)
			while (unzoomCoroutine.MoveNext())
				yield return unzoomCoroutine.Current;
	}

	public static IEnumerator Tilt(TwitchModule module, object yield, string direction, float tiltAngle)
	{
		float easeCubic(float t) { return 3 * t * t - 2 * t * t * t; }

		Dictionary<string[], int> directionNames = new Dictionary<string[], int>()
		{
			{ new[] { "up", "u", "top", "t" }, 0 },
			{ new[] { "upright", "rightup", "ur", "ru", "topright", "righttop", "tr", "rt" }, 45 },
			{ new[] { "right", "r" }, 90 },
			{ new[] { "downright", "rightdown", "dr", "rd", "bottomright", "rightbottom", "br", "rb" }, 135 },
			{ new[] { "down", "d", "bottom", "b" }, 180 },
			{ new[] { "downleft", "leftdown", "dl", "ld", "bottomleft", "leftbottom", "bl", "lb" }, 255 },
			{ new[] { "left", "l" }, 270 },
			{ new[] { "upleft", "leftup", "ul", "lu", "topleft", "lefttop", "tl", "lt" }, 315 },
		};

		var targetRotation = 180;
		if (!string.IsNullOrEmpty(direction))
		{
			var nameAngle = directionNames.Where(pair => pair.Key.Contains(direction)).Select(pair => pair.Value);
			if (nameAngle.Any())
			{
				targetRotation = nameAngle.First();
			}
			else if (int.TryParse(direction, out int directionAngle))
			{
				targetRotation = directionAngle;
			}
			else
			{
				yield break;
			}
		}

		IEnumerator focusCoroutine = module.Bomb.Focus(module.Selectable, module.FocusDistance, module.FrontFace, false);
		while (focusCoroutine.MoveNext())
			yield return focusCoroutine.Current;

		module.HideBanner(0.5f);
		yield return new WaitForSeconds(0.5f);

		var targetAngle = Quaternion.Euler(new Vector3(-Mathf.Cos(targetRotation * Mathf.Deg2Rad), 0, Mathf.Sin(targetRotation * Mathf.Deg2Rad)) * (module.FrontFace ? tiltAngle : -tiltAngle));
		foreach (float alpha in 1f.TimedAnimation())
		{
			var lerp = Quaternion.Lerp(Quaternion.identity, targetAngle, easeCubic(alpha));
			var bombLerp = module.FrontFace ? lerp : Quaternion.Euler(Vector3.Scale(lerp.eulerAngles, new Vector3(1, 1, -1)));
			module.Bomb.RotateByLocalQuaternion(bombLerp);
			TwitchBomb.RotateCameraByLocalQuaternion(module.BombComponent.gameObject, lerp);
			yield return null;
		}

		yield return yield is float delay ? new WaitForSecondsWithCancel(delay, false, module.Solver) : yield;

		if (CoroutineCanceller.ShouldCancel)
		{
			var angle = Quaternion.identity;
			var bombAngle = module.FrontFace ? angle : Quaternion.Euler(Vector3.Scale(angle.eulerAngles, new Vector3(1, 1, -1)));
			module.Bomb.RotateByLocalQuaternion(bombAngle);
			TwitchBomb.RotateCameraByLocalQuaternion(module.BombComponent.gameObject, angle);
			module.StartCoroutine(module.Bomb.Defocus(module.Selectable, module.FrontFace, false));
			module.ShowBanner(0.0f);
			yield break;
		}

		foreach (float alpha in 1f.TimedAnimation())
		{
			var lerp = Quaternion.Lerp(targetAngle, Quaternion.identity, easeCubic(alpha));
			var bombLerp = module.FrontFace ? lerp : Quaternion.Euler(Vector3.Scale(lerp.eulerAngles, new Vector3(1, 1, -1)));
			module.Bomb.RotateByLocalQuaternion(bombLerp);
			TwitchBomb.RotateCameraByLocalQuaternion(module.BombComponent.gameObject, lerp);
			yield return null;
		}

		module.ShowBanner(0.5f);
		IEnumerator defocusCoroutine = module.Bomb.Defocus(module.Selectable, module.FrontFace, false);
		while (defocusCoroutine.MoveNext())
			yield return defocusCoroutine.Current;

		yield return new WaitForSeconds(0.5f);
	}

	/// <name>Zoom, Superzoom, Show and Tilt</name>
	/// <syntax>zoom (duration) (command)\nsuperzoom (factor) (x) (y) (duration) (command)\ntilt (direction) (angle) (command)\nshow</syntax>
	/// <summary>x秒モジュールを拡大する。コマンドを繋げて、ズームしながらコマンドを実行することも出来る。
	///  superzoomはズームをよりコントロールできる。(factor)は倍率、(x)と(y)はカメラの位置をコントロールする。(0, 0)は左下、(1, 1)は右上に対応する。例：superzoom 3 0.5 0.5 20 = 3倍で(0.5 0.5、中央)を20秒間ズームする。
	/// Tiltはモジュールのカメラをその方向に傾けて見やすくする。傾ける方向は8方向の英単語もしくは任意の数(0=真上、時計回りにすすめる)を指定できる。(angle)は傾きの度合いを0~90で設定する。例：tilt 60 90 = 時計回り60度方向から真横まで傾ける。
	/// Showコマンドは爆弾を選択する。
	/// Zoom Tilt、Superzoom Tilt、Zoom Show、Superzoom Showは繋げて入力することで、同時に行うことが出来る。
	/// </summary>
	[Command(null)]
	public static IEnumerator DefaultCommand(TwitchModule module, string user, string cmd)
	{
		if (((Votes.Active && Votes.CurrentVoteType == VoteTypes.Solve && Votes.voteModule == module) || module.Votesolving) && !TwitchPlaySettings.data.AnarchyMode)
		{
			IRCConnection.SendMessage($"@{user}さん：操作しようとしているモジュールは、自動解除中です。");
			yield break;
		}
		if (cmd.RegexMatch(out Match match, @"(?:(?<zoom>zoom *(?<time>\d*\.?\d+)?)|(?<superzoom>superzoom *(?<factor>\d*\.?\d+) *(?<x>\d*\.?\d+)? *(?<y>\d*\.?\d+)? *(?<stime>\d*\.?\d+)?))? *(?:(?<tilt>tilt *(?<direction>[uptobmdwnlefrigh]+|-?\d+)? *(?<angle>\d*\.?\d+)?)|(?<show>show)?)? *(?:send *to *module)? *(?<command>.+)?"))
		{
			var groups = match.Groups;
			var timed = groups["time"].Success || groups["stime"].Success;
			var zooming = groups["zoom"].Success || groups["superzoom"].Success;
			var tilt = groups["tilt"].Success;
			var show = groups["show"].Success;
			var command = groups["command"].Success;

			if (!timed && !zooming && !command && show)
			{
				yield return Show(module, 0.5);
				yield break;
			}
			// Either a zoom, show or tilt needs to take place otherwise, we should let the command run normally.
			if (zooming || tilt || show)
			{
				MusicPlayer musicPlayer = null;
				float delay = 2;
				if (timed)
				{
					delay = groups["time"].Value.TryParseFloat() ?? groups["stime"].Value.TryParseFloat() ?? 2;
					delay = Math.Max(2, delay);
				}

				List<object> yields = new List<object>();
				if (command) yields.Add(RunModuleCommand(module, user, groups["command"].Value));
				if (timed || !command) yields.Add(new WaitForSecondsWithCancel(delay, false, module.Solver));
				IEnumerator toYield = yields.GetEnumerator();

				IEnumerator routine = Show(module, toYield);
				if (tilt)
				{
					var tiltAngle = Mathf.Min(groups["angle"].Value.TryParseFloat() ?? 60, 90);
					routine = Tilt(module, toYield, groups["direction"].Value.ToLowerInvariant(), tiltAngle);
				}

				if (zooming)
				{
					var zoomData = new SuperZoomData(
						groups["factor"].Value.TryParseFloat() ?? 1,
						groups["x"].Value.TryParseFloat() ?? 0.5f,
						groups["y"].Value.TryParseFloat() ?? 0.5f
					);
					routine = Zoom(module, zoomData, routine ?? toYield);
				}

				if (delay >= 15)
					musicPlayer = MusicPlayer.StartRandomMusic();

				yield return routine;
				if (CoroutineCanceller.ShouldCancel)
				{
					CoroutineCanceller.ResetCancel();
					IRCConnection.SendMessage($"@{user}さん：{delay}秒間の操作時間は中断されました。");
				}

				if (musicPlayer != null)
					musicPlayer.StopMusic();

				yield break;
			}
		}

		yield return RunModuleCommand(module, user, cmd);
	}

	private static IEnumerator RunModuleCommand(TwitchModule module, string user, string cmd)
	{
		if (module.Solver == null)
			yield break;

		if (module.Solved && !TwitchPlaySettings.data.AnarchyMode)
		{
			IRCConnection.SendMessageFormat(TwitchPlaySettings.data.AlreadySolved, module.Code, module.PlayerName, user, module.HeaderText);
			yield break;
		}

		if (module.Bomb.BackdoorComponent != null && module.Bomb.BackdoorComponent.GetValue<bool>("BeingHacked") && module.BombComponent.GetModuleDisplayName() != "Backdoor Hacking")
		{
			IRCConnection.SendMessageFormat(TwitchPlaySettings.data.BackdoorHackingBlock, module.Code, user, module.HeaderText);
			yield break;
		}

		Transform tsLight = module.BombComponent.StatusLightParent?.transform.Find("statusLight(Clone)").Find("Component_LED_ERROR(Clone)");
		if (tsLight != null && tsLight.gameObject.activeSelf)
		{
			IRCConnection.SendMessageFormat(TwitchPlaySettings.data.TechSupportBlock, module.Code, user, module.HeaderText);
			yield break;
		}

		// We’re allowed to interact with this module if either:
		if (
			// the module is unclaimed;
			module.PlayerName == null ||
			// the module is claimed by the player;
			module.PlayerName == user ||
			// anarchy mode is on;
			TwitchPlaySettings.data.AnarchyMode ||
			// there is less than X time left on the clock;
			module.Bomb.CurrentTimer <= TwitchPlaySettings.data.MinTimeLeftForClaims ||
			// there are only X unsolved modules left.
			TwitchGame.Instance.Modules.Count(x => !x.Solved && GameRoom.Instance.IsCurrentBomb(x.BombID)) < TwitchPlaySettings.data.MinUnsolvedModulesLeftForClaims
		)
		{
			var response = module.Solver.RespondToCommand(user, cmd);
			while (response.MoveNext())
				yield return response.Current;

			module.Solver.EnableAnarchyStrike();
		}
		else
			IRCConnection.SendMessageFormat(TwitchPlaySettings.data.AlreadyClaimed, module.Code, module.PlayerName, user, module.HeaderText);
	}

	#region Private methods
	private static void ClaimViewOrPin(TwitchModule module, string user, bool isWhisper, bool view, bool pin)
	{
		if (isWhisper)
		{
			IRCConnection.SendMessage($"@{user}さん：ウィスパーコメントでは、モジュールの割り当ては行えません。", user, false);
			return;
		}

		IRCConnection.SendMessage(module.TryClaim(user, view, pin).Message);
	}
	#endregion
}

public struct SuperZoomData
{
	public float factor;
	public float x;
	public float y;

	public SuperZoomData(float factor = 1, float x = 0.5f, float y = 0.5f)
	{
		this.factor = Math.Max(factor, 0.1f);
		this.x = Math.Max(Math.Min(x, 1), 0);
		this.y = Math.Max(Math.Min(y, 1), 0);
	}
}