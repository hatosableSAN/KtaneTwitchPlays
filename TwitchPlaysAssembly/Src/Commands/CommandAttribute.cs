using System;
using JetBrains.Annotations;

/// <summary>Twitch Playsシステムが理解できるコマンドの分類</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
[MeansImplicitUse]
sealed class CommandAttribute : Attribute
{
	/// <summary>
	///     コマンドのすべての形式にマッチする正規表現。コマンドのどの部分にマッチするかに注意すること。
	///     この正規表現は、コマンドのタイプ（共通、モジュール、 爆弾、持ち上げ可能物）によって異なる。</summary>
	public string Regex { get; }

	/// <summary>このコマンドを使用するために必要なアクセス権限。</summary>
	public AccessLevel AccessLevel { get; }

	/// <summary>アナーキーモード中にこのコマンドを使用するために必要なアクセス権限。</summary>
	public AccessLevel AccessLevelAnarchy { get; }

	/// <summary>Constructor.</summary>
	public CommandAttribute(string regex, AccessLevel accessLevel = AccessLevel.User, AccessLevel accessLevelAnarchy = AccessLevel.User)
	{
		Regex = regex == null ? null : $"^{regex}$";
		AccessLevel = accessLevel;
		AccessLevelAnarchy = accessLevelAnarchy;
	}
}

/// <summary>モジュールのコマンドを、そのモジュールが解除済みであっても使用できるコマンドとしてマークする。</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
sealed class SolvedAllowedAttribute : Attribute { }

/// <summary>エレベータールームでのみ使用可能な爆弾コマンドまたはゲームコマンドを指定する。</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
sealed class ElevatorOnlyAttribute : Attribute { }
/// <summary>エレベータールームで使用できない爆弾コマンドやゲームコマンドを指定する。</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
sealed class ElevatorDisallowedAttribute : Attribute { }

/// <summary>EnableDebuggingCommandsが有効な場合にのみ使用できるコマンドを指定する。</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
sealed class DebuggingOnlyAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
sealed class GroupAttribute : Attribute
{
	public int GroupIndex { get; }
	public GroupAttribute(int groupIndex) { GroupIndex = groupIndex; }
}
