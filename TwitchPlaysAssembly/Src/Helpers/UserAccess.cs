using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
using Formatting = Newtonsoft.Json.Formatting;

[Flags]
public enum AccessLevel
{
	User = 0x0000,
	NoPoints = 0x0001,
	Banned = 0x0002,
	Defuser = 0x0004,
	ScoringManager = 0x0008,

	Mod = 0x2000,
	Admin = 0x4000,
	SuperUser = 0x8000,
	Streamer = 0x10000
}

public class BanData
{
	public string BannedBy;
	public string BannedReason;
	public double BanExpiry;
}

public static class UserAccess
{
	private class UserAccessData
	{
		public bool StickyBans = false;
		public AccessLevel MinimumAccessLevelForBanCommand = AccessLevel.Mod;
		public AccessLevel MinimumAccessLevelForTimeoutCommand = AccessLevel.Mod;
		public AccessLevel MinimumAccessLevelForUnbanCommand = AccessLevel.Mod;
		public Dictionary<string, AccessLevel> UserAccessLevel = new Dictionary<string, AccessLevel>();

		public Dictionary<string, BanData> Bans = new Dictionary<string, BanData>();

		public static UserAccessData Instance
		{
			get => _instance ?? (_instance = new UserAccessData());
			set => _instance = value;
		}
		private static UserAccessData _instance;
	}

	static UserAccess()
	{
		/*
		 * Enter here the list of special user roles, giving them bitwise enum flags to determine the level of access each user has.
		 * 
		 * The access level enum can be extended further per your requirements.
		 * 
		 * Use the helper method below to determine if the user has access for a particular access level or not.
		 */

		//Twitch Usernames can't actually begin with an underscore, so these are safe to include as examples
		UserAccessData.Instance.UserAccessLevel["_UserNickName1".ToLowerInvariant()] = AccessLevel.SuperUser | AccessLevel.Admin | AccessLevel.Mod;
		UserAccessData.Instance.UserAccessLevel["_UserNickName2".ToLowerInvariant()] = AccessLevel.Mod;

		LoadAccessList();
	}

	public static void WriteAccessList()
	{
		string path = Path.Combine(Application.persistentDataPath, UsersSavePath);
		try
		{
			DebugHelper.Log($"Writing User Access information data to file: {path}");
			File.WriteAllText(path, JsonConvert.SerializeObject(UserAccessData.Instance, Formatting.Indented, new StringEnumConverter()));
		}
		catch (Exception ex)
		{
			DebugHelper.LogException(ex);
		}
	}

	public static void LoadAccessList()
	{
		string path = Path.Combine(Application.persistentDataPath, UsersSavePath);
		//Try to read old format first.
		try
		{
			DebugHelper.Log($"Loading User Access information data from file: {path}");
			UserAccessData.Instance.UserAccessLevel = JsonConvert.DeserializeObject<Dictionary<string, AccessLevel>>(File.ReadAllText(path), new StringEnumConverter());
			UserAccessData.Instance.UserAccessLevel = UserAccessData.Instance.UserAccessLevel.ToDictionary(pair => pair.Key.ToLowerInvariant(), pair => pair.Value);
			WriteAccessList();
			return;
		}
		catch (FileNotFoundException)
		{
			DebugHelper.LogWarning($"File {path} was not found.");
			WriteAccessList();
			return;
		}
		catch (Exception ex)
		{
			try
			{
				UserAccessData.Instance = JsonConvert.DeserializeObject<UserAccessData>(File.ReadAllText(path), new StringEnumConverter());
			}
			catch (FileNotFoundException)
			{
				DebugHelper.LogWarning($"UserAccess: File {path} was not found.");
				WriteAccessList();
			}
			catch (Exception ex2)
			{
				DebugHelper.Log("Failed to load AccessLevels.Json in both the Old AND new format, Here are the stack traces.");
				DebugHelper.LogException(ex, "Old AccessLevels.Json format exception:");
				DebugHelper.LogException(ex2, "New AccessLevels.Json format exception:");
			}
		}

		UserAccessData.Instance.UserAccessLevel = UserAccessData.Instance.UserAccessLevel.ToDictionary(pair => pair.Key.ToLowerInvariant(), pair => pair.Value);
		UserAccessData.Instance.Bans = UserAccessData.Instance.Bans.ToDictionary(pair => pair.Key.ToLowerInvariant(), pair => pair.Value);

		foreach (string username in UserAccessData.Instance.UserAccessLevel.Keys
			.Where(x => HasAccess(x, AccessLevel.Banned)).ToArray())
			IsBanned(username);
	}
	public static string UsersSavePath = "AccessLevels.json";

	public static bool ModeratorsEnabled = true;

	public static bool HasAccess(string userNickName, AccessLevel accessLevel, bool orHigher = false)
	{
		if (userNickName == TwitchPlaySettings.data.TwitchPlaysDebugUsername)
			return true;
		if (!UserAccessData.Instance.UserAccessLevel.TryGetValue(userNickName.ToLowerInvariant(),
			out AccessLevel userAccessLevel))
			return accessLevel == AccessLevel.User;
		if (userAccessLevel == accessLevel)
			return true;

		do
		{
			if ((accessLevel & userAccessLevel) == accessLevel &&
				(ModeratorsEnabled || accessLevel < (AccessLevel) 0x2000 || accessLevel == AccessLevel.Streamer))
				return true;
			userAccessLevel = (AccessLevel) ((int) userAccessLevel >> 1);
		} while (userAccessLevel != AccessLevel.User && orHigher);

		return TwitchPlaySettings.data.AnarchyMode && userAccessLevel == AccessLevel.Defuser;
	}

	public static AccessLevel HighestAccessLevel(string userNickName)
	{
		if (userNickName == TwitchPlaySettings.data.TwitchPlaysDebugUsername) return AccessLevel.Streamer;

		if (TwitchGame.Instance.Bombs.Any(x => x.BombName == userNickName)) return AccessLevel.Streamer;

		if (!UserAccessData.Instance.UserAccessLevel.TryGetValue(userNickName.ToLowerInvariant(),
			out AccessLevel userAccessLevel))
			return AccessLevel.User;
		if (IsBanned(userNickName) != null)
			return AccessLevel.Banned;
		for (AccessLevel level = (AccessLevel) 0x40000000; level > 0; level = (AccessLevel) ((int) level >> 1))
			if ((userAccessLevel & level) == level)
				return level;
		return TwitchPlaySettings.data.AnarchyMode ? AccessLevel.Defuser : AccessLevel.User;
	}

	public static void TimeoutUser(string userNickName, string moderator, string reason, int timeout, bool isWhisper)
	{
		userNickName = userNickName.FormatUsername();
		if (!HasAccess(moderator, UserAccessData.Instance.MinimumAccessLevelForTimeoutCommand, true))
		{
			IRCConnection.SendMessage($"@{moderator}さん：このコマンドの実行に必要な権限がありません。", moderator, !isWhisper);
			return;
		}
		if (timeout <= 0)
		{
			IRCConnection.SendMessage("使用方法：!timeout <ユーザー名> <秒数> [理由]。秒数は1秒以上を指定する。", moderator, !isWhisper);
			return;
		}
		if (HasAccess(userNickName, AccessLevel.Streamer))
		{
			IRCConnection.SendMessage($"@{moderator}さん：配信者をタイムアウトすることはできません。", moderator, !isWhisper);
			return;
		}
		if (userNickName.EqualsIgnoreCase(moderator.ToLowerInvariant()))
		{
			IRCConnection.SendMessage($"@{moderator}さん：自分自身をタイムアウトすることはできません。", moderator, !isWhisper);
			return;
		}
		AddUser(userNickName, AccessLevel.Banned);
		if (!UserAccessData.Instance.Bans.TryGetValue(userNickName.ToLowerInvariant(), out BanData ban))
			ban = new BanData();
		ban.BannedBy = moderator;
		ban.BannedReason = reason;
		ban.BanExpiry = DateTime.Now.TotalSeconds() + timeout;
		UserAccessData.Instance.Bans[userNickName.ToLowerInvariant()] = ban;

		WriteAccessList();
		IRCConnection.SendMessage($"ユーザー「{userNickName}」は、{(reason == null ? "" : $"理由「{reason}」のため、")} {timeout} 秒間コマンドの使用が一時的に制限されています。コマンドの使用許可をモデレーターにリクエストすることができます。");
		if (TwitchPlaySettings.data.EnableWhispers)
			IRCConnection.SendMessage($"ユーザー「{userNickName}」は、{(reason == null ? "" : $"理由「{reason}」のため、")} {timeout} 秒間コマンドの使用が一時的に制限されています。コマンドの使用許可をモデレーターにリクエストすることができます。", userNickName, false);
	}

	public static void BanUser(string userNickName, string moderator, string reason, bool isWhisper)
	{
		userNickName = userNickName.FormatUsername();
		if (!HasAccess(moderator, UserAccessData.Instance.MinimumAccessLevelForBanCommand, true))
		{
			IRCConnection.SendMessage($"@{moderator}さん：このコマンドの実行に必要な権限がありません。", moderator, !isWhisper);
			return;
		}
		if (HasAccess(userNickName, AccessLevel.Streamer))
		{
			IRCConnection.SendMessage($"@{moderator}さん：配信者をBANすることはできません。", moderator, !isWhisper);
			return;
		}
		if (userNickName.EqualsIgnoreCase(moderator))
		{
			IRCConnection.SendMessage($"@{moderator}さん：自分自身をBANすることはできません。", moderator, !isWhisper);
			return;
		}
		AddUser(userNickName, AccessLevel.Banned);
		if (!UserAccessData.Instance.Bans.TryGetValue(userNickName.ToLowerInvariant(), out BanData ban))
			ban = new BanData();
		ban.BannedBy = moderator;
		ban.BannedReason = reason;
		ban.BanExpiry = double.PositiveInfinity;
		UserAccessData.Instance.Bans[userNickName.ToLowerInvariant()] = ban;
		WriteAccessList();
		IRCConnection.SendMessage($"ユーザー「{userNickName}」は、{(reason == null ? "" : $"理由「{reason}」のため、")} コマンドの使用が一時的に制限されています。コマンドの使用許可をモデレーターにリクエストすることができます。");
		if (TwitchPlaySettings.data.EnableWhispers)
			IRCConnection.SendMessage($"ユーザー「{userNickName}」は、{(reason == null ? "" : $"理由「{reason}」のため、")} コマンドの使用が一時的に制限されています。コマンドの使用許可をモデレーターにリクエストすることができます。", userNickName, false);
	}

	private static void UnbanUser(string userNickName, bool rewrite = true)
	{
		RemoveUser(userNickName, AccessLevel.Banned);
		if (UserAccessData.Instance.Bans.ContainsKey(userNickName.ToLowerInvariant()))
			UserAccessData.Instance.Bans.Remove(userNickName.ToLowerInvariant());
		if (rewrite)
			WriteAccessList();
	}

	public static void UnbanUser(string userNickName, string moderator, bool isWhisper)
	{
		userNickName = userNickName.FormatUsername();
		if (!HasAccess(moderator, UserAccessData.Instance.MinimumAccessLevelForUnbanCommand, true))
		{
			IRCConnection.SendMessage($"@{moderator}さん：このコマンドの実行に必要な権限がありません。", moderator, !isWhisper);
			return;
		}
		UnbanUser(userNickName);
		IRCConnection.SendMessage($"{userNickName}さんは{moderator}によって、BANが解除されました。");
		if (TwitchPlaySettings.data.EnableWhispers)
			IRCConnection.SendMessage($"あなたは{moderator}によって、BANが解除されました。", userNickName, false);
	}

	public static BanData IsBanned(string userNickName)
	{
		userNickName = userNickName.FormatUsername();
		if (!UserAccessData.Instance.Bans.TryGetValue(userNickName.ToLowerInvariant(), out BanData ban) || !HasAccess(userNickName, AccessLevel.Banned))
		{
			bool rewrite = ban != null;
			rewrite |= HasAccess(userNickName, AccessLevel.Banned);
			UnbanUser(userNickName, rewrite);
			return null;
		}

		bool unban = ban.BanExpiry < DateTime.Now.TotalSeconds();
		if (!string.IsNullOrEmpty(ban.BannedBy) && !UserAccessData.Instance.StickyBans)//BAN実施者がいない&stickyban=false
		{
			if (double.IsInfinity(ban.BanExpiry) && !HasAccess(ban.BannedBy, UserAccessData.Instance.MinimumAccessLevelForBanCommand, true))//永続BANかつユーザーがいない
			{
				unban = true;
				IRCConnection.SendMessage($"「{ban.BannedBy}」は権限を失ったため、{userNickName}さんのBANは解除されました。");
				if (TwitchPlaySettings.data.EnableWhispers)
					IRCConnection.SendMessage($"「{ban.BannedBy}」は権限を失ったため、あなたのBANは解除されました。", userNickName, false);
			}
			if (!double.IsInfinity(ban.BanExpiry) && !HasAccess(ban.BannedBy, UserAccessData.Instance.MinimumAccessLevelForTimeoutCommand, true))
			{
				unban = true;
				IRCConnection.SendMessage($"「{ban.BannedBy}」は権限を失ったため、{userNickName}さんのタイムアウトは解除されました。");
				if (TwitchPlaySettings.data.EnableWhispers)
					IRCConnection.SendMessage($"「{ban.BannedBy}」は権限を失ったため、あなたのBANは解除されました。", userNickName, false);
			}
		}
		else if (!UserAccessData.Instance.StickyBans && !unban)//一時BANかつユーザーがいない
		{
			IRCConnection.SendMessage($"BAN実施者が存在しなくなったため、{userNickName}さんのBANは解除されました。");
			if (TwitchPlaySettings.data.EnableWhispers)
				IRCConnection.SendMessage("BAN実施者が存在しなくなったため、あなたのBANは解除されました。", userNickName, false);
			unban = true;
		}
		else
		{
			ban.BannedBy = IRCConnection.Instance.ChannelName;
		}

		unban |= HasAccess(userNickName, UserAccessData.Instance.MinimumAccessLevelForUnbanCommand)
					  || userNickName.EqualsIgnoreCase(ban.BannedBy);

		if (unban)
			UnbanUser(userNickName);

		return TwitchPlaySettings.data.AnarchyMode || unban ? null : ban;
	}

	public static void AddUser(string userNickName, AccessLevel level)
	{
		UserAccessData.Instance.UserAccessLevel.TryGetValue(userNickName.ToLowerInvariant(), out AccessLevel userAccessLevel);
		userAccessLevel |= level;
		UserAccessData.Instance.UserAccessLevel[userNickName.ToLowerInvariant()] = userAccessLevel;
	}

	public static void RemoveUser(string userNickName, AccessLevel level)
	{
		UserAccessData.Instance.UserAccessLevel.TryGetValue(userNickName.ToLowerInvariant(), out AccessLevel userAccessLevel);
		userAccessLevel &= ~level;
		UserAccessData.Instance.UserAccessLevel[userNickName.ToLowerInvariant()] = userAccessLevel;
	}

	public static Dictionary<string, AccessLevel> GetUsers() => UserAccessData.Instance.UserAccessLevel;

	public static Dictionary<string, BanData> GetBans() => UserAccessData.Instance.Bans;

	public static string LevelToString(AccessLevel level)
	{
		switch (level)
		{
			case AccessLevel.Banned:
				return "BAN済";
			case AccessLevel.User:
				return "ユーザー";
			case AccessLevel.NoPoints:
				return "ポイントなし";
			case AccessLevel.Defuser:
				return "処理担当者";
			case AccessLevel.ScoringManager:
				return "スコア管理者";
			case AccessLevel.Mod:
				return "モデレーター";
			case AccessLevel.Admin:
				return "管理者";
			case AccessLevel.SuperUser:
				return "スーパーユーザー";
			case AccessLevel.Streamer:
				return "配信者";
			default:
				return null;
		}
	}
}

public static class AuditLog
{
	public static void SetupLog()
	{
		string path = Path.Combine(Application.persistentDataPath, SavePath);
		if (!File.Exists(path))
			File.Create(path);
	}

	public static void Log(string username, AccessLevel level, string command)
	{
		File.AppendAllText(Path.Combine(Application.persistentDataPath, SavePath), $"[{DateTime.Now}] {username} ({UserAccess.LevelToString(level)}): {command}{Environment.NewLine}");
	}

	public static string SavePath = "AuditLog.txt";
}
