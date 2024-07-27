using System.Collections;
using UnityEngine;

/// <summary>ゲーム終了後の画面で使えるコマンド</summary>
public static class PostGameCommands
{
	#region Commands
	/// <name>Continue / Back</name>
	/// <syntax>continue\nback</syntax>
	/// <summary>続ける/戻るボタンを押す。</summary>
	[Command(@"(continue|back)")]
	public static IEnumerator Continue() => DoButton(Object.FindObjectOfType<ResultPage>()?.ContinueButton);

	/// <name>Retry</name>
	/// <syntax>retry</syntax>
	/// <summary>ミッションをリトライする。</summary>
	[Command(@"retry")]
	public static IEnumerator Retry(string user, bool isWhisper)
	{
		var resultPage = Object.FindObjectOfType<ResultPage>();
		if (!TwitchGame.RetryAllowed)
		{
			IRCConnection.SendMessage(TwitchPlaySettings.data.RetryModeOrProfileChange, user, isWhisper);
			return DoButton(resultPage.ContinueButton);
		}
		if ((!TwitchPlaySettings.data.EnableRetryButton && !TwitchPlaySettings.data.AnarchyMode) || resultPage.RetryButton == null)
		{
			IRCConnection.SendMessage(TwitchPlaySettings.data.RetryInactive, user, isWhisper);
			return DoButton(resultPage.ContinueButton);
		}
		else
		{
			TwitchPlaySettings.SetRetryReward();
			return DoButton(resultPage.RetryButton);
		}
	}
	#endregion

	private static IEnumerator DoButton(Selectable btn)
	{
		// Press the button
		btn.Trigger();
		yield return null;

		// If the button was pressed while the text “Time remaining: XXX” was still appearing,
		// the button doesn’t actually trigger but instead shortcuts this typing.
		// Therefore, just press the button a second time.
		btn.Trigger();
	}

	public static IEnumerator AutoContinue()
	{
		if (TwitchPlaySettings.data.AutoReturnToSetupMinutes <= 0f)
			yield break;
		yield return new WaitForSeconds(TwitchPlaySettings.data.AutoReturnToSetupMinutes * 60f);

		IRCConnection.SendMessage(TwitchPlaySettings.data.ReturningAutomatically);
		yield return Continue();
	}
}
