using System.Collections;
using System.Reflection;
using Assets.Scripts.Props;

/// <summary>目覚まし時計用コマンド</summary>
/// <prefix>alarm </prefix>
public static class AlarmClockCommands
{
	/// <name>Snooze</name>
	/// <syntax>snooze</syntax>
	/// <summary>スヌーズボタンを押す</summary>
	[Command("snooze")]
	public static IEnumerator Snooze(TwitchHoldable holdable, string user, bool isWhisper) =>
		holdable.RespondToCommand(user, "", isWhisper, Snooze(holdable.Holdable.GetComponent<AlarmClock>(), 1));

	/// <name>Snooze Multiple</name>
	/// <syntax>snooze [times]</syntax>
	/// <summary>指定回数スヌーズを押す。上限50回。</summary>
	[Command(@"snooze +(\d+)")]
	public static IEnumerator SnoozeMultiple(TwitchHoldable holdable, string user, bool isWhisper, [Group(1)] int times) =>
		holdable.RespondToCommand(user, "", isWhisper, Snooze(holdable.Holdable.GetComponent<AlarmClock>(), times));

	public static IEnumerator Snooze(AlarmClock clock, int times = 1)
	{
		var onField = typeof(AlarmClock).GetField("isOn", BindingFlags.NonPublic | BindingFlags.Instance);
		if (onField == null) yield break;

		if (times < 1 || times > 50)
			yield break;

		if (TwitchPlaySettings.data.AllowSnoozeOnly && !TwitchPlaySettings.data.AnarchyMode && !(bool) onField.GetValue(clock))
			yield break;

		yield return null;
		for (int i = 0; i < times; i++)
		{
			clock.SnoozeButton.GetComponent<Selectable>().Trigger();
			yield return new WaitForSecondsWithCancel(.1f);
		}
	}
}
