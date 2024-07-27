using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>爆弾に関するコマンド</summary>
/// <prefix>bomb </prefix>
public static class BombCommands
{
	#region Commands
	/// <name>Help</name>
	/// <syntax>help</syntax>
	/// <summary>いろんなヘルプを表示する。</summary>
	[Command("help")]
	public static void Help(string user, bool isWhisper) => IRCConnection.SendMessage(TwitchPlaySettings.data.BombHelp, user, !isWhisper);

	/// <name>Turn</name>
	/// <syntax>turn</syntax>
	/// <summary>爆弾を裏返しにさせる。</summary>
	[Command(@"(turn|turn round|turn around|rotate|flip|spin)")]
	public static IEnumerator TurnBomb(TwitchBomb bomb) => bomb.TurnBomb();

	/// <name>Hold</name>
	/// <syntax>hold</syntax>
	/// <summary>爆弾を持つ</summary>
	[Command(@"(hold|pick up)")]
	public static IEnumerator Hold(TwitchBomb bomb) => bomb.HoldBomb();
	/// <name>Drop</name>
	/// <syntax>drop</syntax>
	/// <summary>爆弾を置く。</summary>
	[Command(@"(drop|let go|put down)")]
	public static IEnumerator Drop(TwitchBomb bomb) => bomb.LetGoBomb();

	/// <name>Throw</name>
	/// <syntax>throw (strength)</syntax>
	/// <summary>爆弾を投げる。数字オプションで、投げる強さを決められる。(デフォルトは5)</summary>
	/// <restriction>Admin</restriction>
	[Command(@"(?:throw|yeet) *(\d+)?", AccessLevel.Admin, AccessLevel.Admin)]
	public static IEnumerator Throw(TwitchBomb bomb, [Group(1)] int? strength = 5)
	{
		yield return HoldableCommands.Throw(bomb.Bomb.GetComponent<FloatingHoldable>(), strength);
	}

	/// <name>Elevator Edgework</name>
	/// <syntax>edgework (wall)</syntax>
	/// <summary>エレベーター内のエッジワークを表示する。壁は左右/背後のいずれかを指定する。</summary>
	/// <restriction>ElevatorOnly</restriction>
	[Command(@"edgework((?: right| left| back| r| l| b)?)"), ElevatorOnly]
	public static IEnumerator EdgeworkElevator(TwitchBomb bomb, [Group(1)] string edge, string user, bool isWhisper) => Edgework(bomb, edge, user, isWhisper);
	/// <name>Edgework</name>
	/// <syntax>edgework (edge)\nedgework 45</syntax>
	/// <summary>爆弾を回転させてエッジワークを表示する。edgeは、爆弾のどの辺を表示するかを示す。45を使うと、爆弾を45度回転させる。</summary>
	/// <restriction>ElevatorDisallowed</restriction>
	[Command(@"edgework((?: 45|-45)|(?: top right| right top| right bottom| bottom right| bottom left| left bottom| left top| top left| left| top| right| bottom| tr| rt| tl| lt| br| rb| bl| lb| t| r| b| l))?"), ElevatorDisallowed]
	public static IEnumerator Edgework(TwitchBomb bomb, [Group(1)] string edge, string user, bool isWhisper)
	{
		if (TwitchPlaySettings.data.EnableEdgeworkCommand || TwitchPlaySettings.data.AnarchyMode)
			return bomb.ShowEdgework(edge);
		else
		{
			IRCConnection.SendMessage(string.Format(TwitchPlaySettings.data.BombEdgework, bomb.EdgeworkText.text), user, !isWhisper);
			return null;
		}
	}

	/// <name>Time</name>
	/// <syntax>time</syntax>
	/// <summary>残り時間を表示する。</summary>
	[Command(@"(timer?|clock)")]
	public static void Time(TwitchBomb bomb, string user, bool isWhisper) => IRCConnection.SendMessage(string.Format(TwitchPlaySettings.data.BombTimeRemaining, bomb.GetFullFormattedTime, bomb.GetFullStartingTime), user, !isWhisper);
	/// <name>Timestamp</name>
	/// <syntax>timestamp</syntax>
	/// <summary>爆弾が起動した日を表示する。</summary>
	[Command(@"(timestamp|date)")]
	public static void Timestamp(TwitchBomb bomb, string user, bool isWhisper) => IRCConnection.SendMessage(string.Format(TwitchPlaySettings.data.BombTimeStamp, bomb.BombTimeStamp), user, !isWhisper);

	/// <name>End Training Mode</name>
	/// <syntax>endtrainingmode</syntax>
	/// <summary>トレーニングモードを終了する。ホワイトリスト登録者であるか、一定数のポイントが必要。</summary>
	[Command(@"endtrainingmode")]
	public static IEnumerator Explode(TwitchBomb bomb, string user, bool isWhisper)
	{
		if (!OtherModes.TrainingModeOn)
		{
			IRCConnection.SendMessage("トレーニングモードは有効ではありません。", user, false, user);
			return null;
		}

		if (isWhisper)//ウィスパーモード廃止のため、このコマンドは使用されない。
		{
			IRCConnection.SendMessage("{0}さん：ウィスパーコメントでは、トレーニングモードを終了させることはできません。", user, false, user);
			return null;
		}

		Leaderboard.Instance.GetRank(user, out var entry);
		if (!UserAccess.HasAccess(user, AccessLevel.Defuser, true) && entry != null && entry.SolveScore < TwitchPlaySettings.data.MinScoreForNewbomb)
		{
			IRCConnection.SendMessage("トレーニングモード終了に必要なポイント数を獲得していません。");
			return null;
		}

		return bomb.DelayBombExplosionCoroutine();
	}

	/// <name>Explode</name>
	/// <syntax>explode</syntax>
	/// <summary>強制爆発させる。</summary>
	/// <restriction>Mod</restriction>
	[Command(@"(explode|detonate|kapow)", AccessLevel.Mod, AccessLevel.Mod)]
	public static IEnumerator Explode(TwitchBomb bomb) => bomb.DelayBombExplosionCoroutine();

	#region Voting
	/// <name>Start a vote</name>
	/// <syntax>vote [action]</syntax>
	/// <summary>強制爆発投票を行う。</summary>
	[Command(@"vote (explode|detonate|kapow)")]
	public static void VoteStart(string user, [Group(1)] bool Detonation) => Votes.StartVote(user, Detonation ? VoteTypes.Detonation : 0);

	/// <name>Vote</name>
	/// <syntax>vote [choice]</syntax>
	/// <summary>はい/いいえで投票する。</summary>
	[Command(@"vote ((yes|voteyea)|(no|votenay))")]
	public static void Vote(string user, [Group(2)] bool yesVote) => GlobalCommands.Vote(user, yesVote);

	/// <name>Remove vote</name>
	/// <syntax>vote remove</syntax>
	/// <summary>投票を削除する。</summary>
	[Command(@"vote remove")]
	public static void RemoveVote(string user) => GlobalCommands.RemoveVote(user);

	/// <name>Time left of vote</name>
	/// <syntax>vote time</syntax>
	/// <summary>残り投票可能時間を表示する。</summary>
	[Command(@"vote time")]
	public static void ShowVoteTime(string user) => GlobalCommands.ShowVoteTime(user);

	/// <name>Cancel vote</name>
	/// <syntax>vote cancel</syntax>
	/// <summary>投票をキャンセルする。</summary>
	/// <restriction>Mod</restriction>
	[Command(@"vote cancel", AccessLevel.Mod, AccessLevel.Mod)]
	public static void CancelVote(string user) => GlobalCommands.CancelVote(user);

	/// <name>Force-end vote</name>
	/// <syntax>vote forceend</syntax>
	/// <summary>投票を強制的に締め切る。</summary>
	/// <restriction>Mod</restriction>
	[Command(@"vote forceend", AccessLevel.Mod, AccessLevel.Mod)]
	public static void ForceEndVote(string user) => GlobalCommands.ForceEndVote(user);
	#endregion

	/// <name>Status</name>
	/// <syntax>status</syntax>
	/// <summary>爆弾の情報(時間、ミス数、解除数)を表示する。</summary>
	[Command(@"(status|info)")]
	public static void Status(TwitchBomb bomb, string user, bool isWhisper)
	{
		int currentReward = TwitchPlaySettings.GetRewardBonus();
		if (OtherModes.TimeModeOn)
		{
			IRCConnection.SendMessage(string.Format(TwitchPlaySettings.data.BombStatusTimeMode, bomb.GetFullFormattedTime, bomb.GetFullStartingTime,
				OtherModes.GetAdjustedMultiplier(), bomb.BombSolvedModules, bomb.BombSolvableModules, currentReward), user, !isWhisper);
		}
		else if (OtherModes.VSModeOn)
		{
			IRCConnection.SendMessage(string.Format(TwitchPlaySettings.data.BombStatusVsMode, bomb.GetFullFormattedTime,
				bomb.GetFullStartingTime, OtherModes.RedHealth, OtherModes.WhiteHealth, currentReward), user, !isWhisper);
		}
		else
		{
			IRCConnection.SendMessage(string.Format(TwitchPlaySettings.data.BombStatus, bomb.GetFullFormattedTime, bomb.GetFullStartingTime,
				bomb.StrikeCount, bomb.StrikeLimit, bomb.BombSolvedModules, bomb.BombSolvableModules, currentReward), user, !isWhisper);
		}
	}

	/// <name>Pause</name>
	/// <syntax>pause</syntax>
	/// <summary>カウントダウンタイマーを一時停止する。</summary>
	/// <restriction>Admin</restriction>
	[Command(@"pause", AccessLevel.Admin)]
	public static void Pause(TwitchBomb bomb)
	{
		if (bomb.Bomb.GetTimer().IsUpdating)
		{
			bomb.Bomb.GetTimer().StopTimer();
		}
	}
	/// <name>Unpause</name>
	/// <syntax>unpause</syntax>
	/// <summary>カウントダウンタイマーを再度動かす。</summary>
	/// <restriction>Admin</restriction>
	[Command(@"(unpause|resume)", AccessLevel.Admin)]
	public static void Unpause(TwitchBomb bomb)
	{
		if (!bomb.Bomb.GetTimer().IsUpdating)
			bomb.Bomb.GetTimer().StartTimer();
	}

	/// <name>Change Timer</name>
	/// <syntax>add time [time]\nsubtract time [time]\nset time [time]</syntax>
	/// <summary>爆弾の時間を加算/減算/設定する。</summary>
	/// <restriction>Admin</restriction>
	[Command(@"(?:add|increase|(subtract|decrease|remove)|(change|set)) +(?:time|t) +(.+)", AccessLevel.Admin, AccessLevel.Admin)]
	public static void ChangeTimer(TwitchBomb bomb, string user, bool isWhisper, [Group(1)] bool negative, [Group(2)] bool direct, [Group(3)] string amount)
	{
		float time = 0;
		var timeLengths = new Dictionary<string, float>()
			{
				{ "ms", 0.001f },
				{ "s", 1 },
				{ "m", 60 },
				{ "h", 3600 },
				{ "d", 86400 },
				{ "w", 604800 },
				{ "y", 31536000 },
			};

		foreach (string part in amount.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
		{
			bool valid = false;
			foreach (string unit in timeLengths.Keys)
			{
				if (!part.EndsWith(unit, StringComparison.InvariantCultureIgnoreCase) || !float.TryParse(part.Substring(0, part.Length - unit.Length), out float length))
					continue;
				time += length * timeLengths[unit];
				valid = true;
				break;
			}

			if (!valid)
			{
				IRCConnection.SendMessage(@"{0}は、有効な値ではありません。", user, !isWhisper, part);
				return;
			}
		}

		time = (float) Math.Round((decimal) time, 2, MidpointRounding.AwayFromZero);
		if (!direct && Math.Abs(time) < 0.01f)
			return;

		bomb.CurrentTimer = direct ? time : negative ? bomb.CurrentTimer - time : bomb.CurrentTimer + time;

		// 時間が負の場合、逆の意味にする。
		bool negativeTime = time < 0 ? !negative : negative;

		IRCConnection.SendMessage(direct
			? $"爆弾のカウントダウンタイマーを{Math.Abs(time < 0 ? 0 : time).FormatTime()}に設定しました。"
			: $"カウントダウンタイマー{(negativeTime ? "から" : "に")}{Math.Abs(time).FormatTime()}{(negativeTime ? "減算" : "加算")}しました。", user, !isWhisper);
	}

	/// <name>Change Strikes / Strike Limit</name>
	/// <syntax>add strikes [strikes]\nsubtract strikes [strikes]\nset strikes [strikes]</syntax>
	/// <summary>ミスの回数を加算/減算/設定する。strikelimitに置き換えるとミスの最大回数を変更できる。</summary>
	/// <restriction>Admin</restriction>
	[Command(@"(?:add|increase|(subtract|decrease|remove)|(change|set)) +(?:(strikes?|s)|strikelimit|sl|maxstrikes?|ms) +(-?\d+)", AccessLevel.Admin, AccessLevel.Admin)]
	public static void ChangeStrikeParameter(TwitchBomb bomb, string user, bool isWhisper, [Group(1)] bool negative, [Group(2)] bool direct, [Group(3)] bool isStrikes, [Group(4)] int amount)
	{
		void setParameter(string thing1, string thing2, int originalAmount, Func<int, int> set)
		{
			// ミスは0以下にならないようにする(サイモンゲームが解けなくなるため)
			var newAmount = set(Math.Max(0, direct ? amount : negative ? originalAmount - amount : originalAmount + amount));

			if (direct)
				IRCConnection.SendMessage(string.Format("{1}を{0}に設定しました。", newAmount, thing1), user, !isWhisper);
			else
			{
				var difference = Math.Abs(newAmount - originalAmount);
				IRCConnection.SendMessage(string.Format(newAmount >= originalAmount ? "{1}を{0}加算しました。" : "{1}を{0}減算しました。", difference, thing2), user, !isWhisper);
			}
		}

		if (isStrikes)
			setParameter("ミスの回数", "ミスの回数", bomb.StrikeCount, am => bomb.StrikeCount = am);
		else    // strike limit
			setParameter("ミスの上限", "ミスの最大回数", bomb.StrikeLimit, am => bomb.StrikeLimit = am);

		TwitchGame.ModuleCameras.UpdateStrikes();
	}
	#endregion
}
