using System.Collections;
using UnityEngine;

/// <summary>持ち上げ可能物に含まれるコマンド(フリープレイケースやバインダーを含む)</summary>
public static class HoldableCommands
{
	#region Commands
	/// <name>Help</name>
	/// <syntax>help</syntax>
	/// <summary>持ち上げ可能物に対して実行可能なコマンドを表示する。</summary>
	[Command("help")]
	public static bool Help(TwitchHoldable holdable, string user, bool isWhisper) => holdable.PrintHelp(user, isWhisper);

	/// <name>Hold</name>
	/// <syntax>hold</syntax>
	/// <summary>持ち上げ可能物を持ち上げる。</summary>
	[Command("(hold|pick up)")]
	public static IEnumerator Hold(TwitchHoldable holdable) => holdable.Hold();

	/// <name>Drop</name>
	/// <syntax>drop</syntax>
	/// <summary>持ち上げ可能物を置く。</summary>
	[Command("(drop|let go|put down)")]
	public static IEnumerator Drop(TwitchHoldable holdable) => holdable.Drop();

	/// <name>Turn</name>
	/// <syntax>turn</syntax>
	/// <summary>持ち上げ可能物を回転させる。</summary>
	[Command(@"(turn|turn round|turn around|rotate|flip|spin)")]
	public static IEnumerator Flip(TwitchHoldable holdable) => holdable.Turn();

	/// <name>Throw</name>
	/// <syntax>throw\nthrow 10</syntax>
	/// <summary>持ち上げ可能物をある強さで投げる。</summary>
	[Command(@"(?:throw|yeet) *(\d+)?", AccessLevel.Admin, AccessLevel.Admin)]
	public static IEnumerator Throw(FloatingHoldable holdable, [Group(1)] int? optionalStrength = 5)
	{
		int strength = optionalStrength ?? 5;

		holdable.Pause();
		Rigidbody rigidbody = holdable.GetComponent<Rigidbody>();
		rigidbody.isKinematic = false;
		rigidbody.useGravity = true;
		rigidbody.velocity = Random.onUnitSphere * rigidbody.mass * strength;
		rigidbody.angularVelocity = Random.onUnitSphere * rigidbody.mass * strength;
		rigidbody.maxAngularVelocity = 100f;
		yield return new WaitForSeconds(2);
		rigidbody.isKinematic = true;
		rigidbody.useGravity = false;
		holdable.Resume();
	}

	[Command(null)]
	public static IEnumerator DefaultCommand(TwitchHoldable holdable, string user, bool isWhisper, string cmd)
	{
		if (holdable.CommandType != null)
			return holdable.RespondToCommand(user, isWhisper);

		return holdable.RespondToCommand(user, cmd, isWhisper);
	}
	#endregion
}
