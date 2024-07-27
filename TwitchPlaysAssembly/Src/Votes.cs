﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum VoteTypes
{
	Detonation,
	VSModeToggle,
	Solve
}

public class VoteData
{
	// Name of the vote (Displayed over !notes3 when in game)
	internal string Name
	{
		get => Votes.CurrentVoteType == VoteTypes.Solve ? $"モジュール{Votes.voteModule.Code} ({Votes.voteModule.TranslatedName})の解除" : _name;
		set => _name = value;
	}

	// Action to execute if the vote passes
	internal Action onSuccess;

	// Checks the validity of a vote
	internal List<Tuple<Func<bool>, string>> validityChecks;

	private string _name;
}

public static class Votes
{
	private static float VoteTimeRemaining = -1f;
	internal static VoteTypes CurrentVoteType;

	public static bool Active => voteInProgress != null;
	internal static int TimeLeft => Mathf.CeilToInt(VoteTimeRemaining);
	internal static int NumVoters => Voters.Count;

	internal static TwitchModule voteModule;

	internal static readonly Dictionary<VoteTypes, VoteData> PossibleVotes = new Dictionary<VoteTypes, VoteData>()
	{
		{
			VoteTypes.Detonation, new VoteData {
				Name = "爆弾を起爆",
				validityChecks = new List<Tuple<Func<bool>, string>>
				{
					CreateCheck(() => TwitchPlaySettings.data.MaxVoteDetonatesPerBomb >= 0 && TwitchGame.Instance.VoteDetonateAttempts >= TwitchPlaySettings.data.MaxVoteDetonatesPerBomb, "{0}さん：この爆弾の起爆投票の上限に到達しました。これ以上開始できません。")
				},
				onSuccess = () => TwitchGame.Instance.Bombs[0].CauseExplosionByVote()
			}
		},
		{
			VoteTypes.VSModeToggle, new VoteData {
				Name = "VSモードの切り替え",
				validityChecks = null,
				onSuccess = () => {
					OtherModes.Toggle(TwitchPlaysMode.VS);
					IRCConnection.SendMessage($"次のモードは、{OtherModes.GetName(OtherModes.nextMode)}になります。");
				}
			}
		},
		{
			VoteTypes.Solve, new VoteData {
				validityChecks = new List<Tuple<Func<bool>, string>>
				{
					CreateCheck(() => !TwitchPlaySettings.data.EnableVoteSolve, "{0}さん：投票による解除は無効に設定されています。"),
					CreateCheck(() => voteModule.Votesolving, "{0}さん：そのモジュールは、すでに投票による解除が行われています。"),
					CreateCheck(() => OtherModes.currentMode == TwitchPlaysMode.VS && !TwitchPlaySettings.data.EnableVSVoteSolve, "{0}さん：VSモード中は、投票による解除は無効に設定されています。"),
					CreateCheck(() => TwitchPlaySettings.data.MaxVoteSolvesPerBomb >= 0 && TwitchGame.Instance.VoteSolveCount >= TwitchPlaySettings.data.MaxVoteSolvesPerBomb, "{0}さん：投票による解除の最大回数に達しました。これ以上は開始できません。"),
					CreateCheck(() =>
						TwitchPlaySettings.data.VoteSolveBossNormalModuleRatio >= float.Epsilon &&
						TwitchPlaySettings.data.VoteSolveBossMinSeconds > 0 &&
						voteModule.BombComponent.GetModuleID().IsBossMod() &&
						((double)TwitchGame.Instance.CurrentBomb.BombSolvedModules / TwitchGame.Instance.CurrentBomb.BombSolvableModules >= TwitchPlaySettings.data.VoteSolveBossNormalModuleRatio ||
						TwitchGame.Instance.CurrentBomb.BombStartingTimer - TwitchGame.Instance.CurrentBomb.CurrentTimer < TwitchPlaySettings.data.VoteSolveBossMinSeconds),
						$"{{0}}さん：ボスモジュールは、爆弾が起動してから{TwitchPlaySettings.data.VoteSolveBossMinSeconds}秒経過後かつ全モジュール中{TwitchPlaySettings.data.VoteSolveBossNormalModuleRatio * 100}%のモジュールが解除される前にのみ、投票による解除を実施できます。"),
					CreateCheck(() =>
						TwitchPlaySettings.data.VoteSolveNonBossRatio >= float.Epsilon &&
						((double)TwitchGame.Instance.CurrentBomb.BombSolvedModuleIDs.Count(x => !x.IsBossMod()) /
						TwitchGame.Instance.CurrentBomb.BombSolvableModuleIDs.Count(x => !x.IsBossMod()) <= TwitchPlaySettings.data.VoteSolveNonBossRatio) &&
						!voteModule.BombComponent.GetModuleID().IsBossMod(),
						$"{{0}}さん：投票による解除を実施するには、ボスモジュールを除く{TwitchPlaySettings.data.VoteSolveNonBossRatio * 100}%のモジュールが解除される必要があります。"),
					CreateCheck(() => voteModule.Claimed, "{0}：投票による解除を実施するには、モジュールの割り当てを解除する必要があります。"),
					CreateCheck(() => voteModule.ClaimQueue.Count > 0, "{0}さん：そのモジュールには、予約済みの割り当てが行われています。"),
					CreateCheck(() => TwitchPlaySettings.data.MinScoreForVoteSolve > 0 && (int)voteModule.ScoreMethods.Sum(x => x.CalculateScore(null)) <= TwitchPlaySettings.data.MinScoreForVoteSolve && !voteModule.BombComponent.GetModuleID().IsBossMod(), $"{{0}}さん：投票による解除を実施するには、モジュールのスコアが{TwitchPlaySettings.data.MinScoreForVoteSolve}ポイントより高くなければいけません。"),
					CreateCheck(() => TwitchGame.Instance.CommandQueue.Any(x => x.Message.Text.StartsWith($"!{voteModule.Code} ")), "{0}さん：そのモジュールには、キュー追加済みのコマンドが含まれています。"),
					CreateCheck(() => !TwitchPlaySettings.data.EnableMissionVoteSolve && GameplayState.MissionToLoad != "custom", "{0}さん：ミッション中は、投票による解除は行えません。")
				},
				onSuccess = () =>
				{
					voteModule.Solver.SolveModule($"モジュール({voteModule.TranslatedName})は自動的に解除されます。");
					voteModule.SetClaimedUserMultidecker("自動解除中");
					voteModule.Votesolving = true;
					if (TwitchPlaySettings.data.VoteSolveRewardDecrease >= float.Epsilon)
					{
						TwitchPlaySettings.SetRewardBonus((TwitchPlaySettings.GetRewardBonus() * (1f - TwitchPlaySettings.data.VoteSolveRewardDecrease)).RoundToInt());
						IRCConnection.SendMessage($"モジュール{voteModule.Code} ({voteModule.TranslatedName})の自動解除により、報酬が{TwitchPlaySettings.data.VoteSolveRewardDecrease * 100}%減少しました。");
					}
				}
			}
		}
	};

	private static readonly Dictionary<string, bool> Voters = new Dictionary<string, bool>();

	private static Coroutine voteInProgress;
	private static IEnumerator VotingCoroutine()
	{
		while (VoteTimeRemaining >= 0f)
		{
			var oldTime = TimeLeft;
			VoteTimeRemaining -= Time.deltaTime;

			if (TwitchGame.BombActive && TimeLeft != oldTime) // Once a second, update notes.
				TwitchGame.ModuleCameras.SetNotes();
			yield return null;
		}

		if (TwitchGame.BombActive && (CurrentVoteType == VoteTypes.Detonation || (CurrentVoteType == VoteTypes.Solve && TwitchPlaySettings.data.EnableVoteSolveAutomaticNoForClaims)))
		{
			// Add claimed users who didn't vote as "no"
			int numAddedNoVotes = 0;
			List<string> usersWithClaims = TwitchGame.Instance.Modules
				.Where(m => !m.Solved && m.PlayerName != null).Select(m => m.PlayerName).Distinct().ToList();
			foreach (string user in usersWithClaims)
			{
				if (!Voters.ContainsKey(user))
				{
					++numAddedNoVotes;
					Voters.Add(user, false);
				}
			}
				IRCConnection.SendMessage($"モジュールを割り当てられている未投票者の投票として、{numAddedNoVotes}名の「No」が追加されました。");
		}

		int yesVotes = Voters.Count(pair => pair.Value);
		bool votePassed = yesVotes >= Voters.Count * (TwitchPlaySettings.data.MinimumYesVotes[CurrentVoteType] / 100f);
		IRCConnection.SendMessage($"賛成票は{yesVotes}/{Voters.Count}で締め切りました。投票は{(votePassed ? "可決" : "否決")}されました。");
		if (!votePassed && CurrentVoteType == VoteTypes.Solve)
		{
			voteModule.SetBannerColor(voteModule.unclaimedBackgroundColor);
			voteModule.SetClaimedUserMultidecker(null);
		}
		if (votePassed)
		{
			PossibleVotes[CurrentVoteType].onSuccess();
		}

		DestroyVote();
	}

	private static void CreateNewVote(string user, VoteTypes act, TwitchModule module = null)
	{
		voteModule = module;
		if (TwitchGame.BombActive && act != VoteTypes.VSModeToggle)
		{
			if (act == VoteTypes.Solve && module == null)
				throw new InvalidOperationException("投票による解除を行うモジュールがNULLです。正常な動作ではないため、TP開発者にログファイルをお送りください！");

			var validity = PossibleVotes[act].validityChecks.Find(x => x.First());
			if (validity != null && !(TwitchPlaySettings.data.AnarchyMode && !voteModule.Votesolving))
			{
				IRCConnection.SendMessage(string.Format(validity.Second, user));
				return;
			}

			switch (act)
			{
				case VoteTypes.Detonation:
					TwitchGame.Instance.VoteDetonateAttempts++;
					break;
				case VoteTypes.Solve:
					if (voteModule is null)
						throw new InvalidOperationException("Votemodule cannot be null");
					TwitchGame.Instance.VoteSolveCount++;
					voteModule.SetBannerColor(voteModule.MarkedBackgroundColor);
					voteModule.SetClaimedUserMultidecker("投票中");
					break;
			}
		}

		CurrentVoteType = act;
		VoteTimeRemaining = TwitchPlaySettings.data.VoteCountdownTime;
		Voters.Clear();
		Voters.Add(user, true);
		IRCConnection.SendMessage($"{user}さんによって「{PossibleVotes[CurrentVoteType].Name}」の投票が開始しました！「!vote yes」または「!vote no」で投票してください。");
		voteInProgress = TwitchPlaysService.Instance.StartCoroutine(VotingCoroutine());
		if (TwitchGame.Instance.alertSound != null)
			TwitchGame.Instance.alertSound.Play();
		if (TwitchGame.BombActive)
			TwitchGame.ModuleCameras.SetNotes();
	}

	private static void DestroyVote()
	{
		if (voteInProgress != null)
			TwitchPlaysService.Instance.StopCoroutine(voteInProgress);
		voteInProgress = null;
		Voters.Clear();
		voteModule = null;
		if (TwitchGame.BombActive)
			TwitchGame.ModuleCameras.SetNotes();
	}

	internal static void OnStateChange()
	{
		// Any ongoing vote ends.
		DestroyVote();
	}

	#region UserCommands
	public static void Vote(string user, bool vote)
	{
		if (!Active)
		{
			IRCConnection.SendMessage($"{user}さん：現在進行中の投票はありません。");
			return;
		}

		if (Voters.ContainsKey(user) && Voters[user] == vote)
		{
			IRCConnection.SendMessage($"{user}さん：すでに{(vote ? "「Yes」" : "「No」")}に投票しています。");
			return;
		}

		Voters[user] = vote;
		IRCConnection.SendMessage($"{user}さんが、「{(vote ? "Yes" : "No")}」に投票しました。");
	}

	public static void RemoveVote(string user)
	{
		if (!Active)
		{
			IRCConnection.SendMessage($"{user}さん：現在進行中の投票はありません。");
			return;
		}

		if (!Voters.ContainsKey(user))
		{
			IRCConnection.SendMessage($"{user}さん：まだ投票していません。");
			return;
		}

		Voters.Remove(user);
		IRCConnection.SendMessage($"{user}さんの投票を削除しました。");
	}
	#endregion

	public static void StartVote(string user, VoteTypes act, TwitchModule module = null)
	{
		if (!TwitchPlaySettings.data.EnableVoting)
		{
			IRCConnection.SendMessage($"{user}さん：投票は無効に設定されています。");
			return;
		}

		if (Active)
		{
			IRCConnection.SendMessage($"{user}さん：すでに進行中の投票があります。");
			return;
		}

		CreateNewVote(user, act, module);
	}

	public static void TimeLeftOnVote(string user)
	{
		if (!Active)
		{
			IRCConnection.SendMessage($"{user}さん：現在進行中の投票はありません。");
			return;
		}
		IRCConnection.SendMessage($"「{PossibleVotes[CurrentVoteType].Name}」への投票は、残り{TimeLeft}秒です。");
	}

	public static void CancelVote(string user)
	{
		if (!Active)
		{
			IRCConnection.SendMessage($"{user}さん：現在進行中の投票はありません。");
			return;
		}
		IRCConnection.SendMessage("投票はキャンセルされました。");
		if (CurrentVoteType == VoteTypes.Solve)
		{
			voteModule.SetBannerColor(voteModule.unclaimedBackgroundColor);
			voteModule.SetClaimedUserMultidecker(null);
		}
		DestroyVote();
	}

	public static void EndVoteEarly(string user)
	{
		if (!Active)
		{
			IRCConnection.SendMessage($"{user}さん：：現在進行中の投票はありません。");
			return;
		}
		IRCConnection.SendMessage("投票を締め切ります。");
		VoteTimeRemaining = 0f;
	}

	private static Tuple<Func<bool>, string> CreateCheck(Func<bool> func, string str) => new Tuple<Func<bool>, string>(func, str);
}