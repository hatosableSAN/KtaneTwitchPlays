using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

public class MotionSenseComponentSolver : ComponentSolver
{
	public MotionSenseComponentSolver(TwitchModule module) :
		base(module)
	{
		_component = module.BombComponent.GetComponent(ComponentType);
		_needy = (KMNeedyModule) NeedyField.GetValue(_component);
		ModInfo = ComponentSolverFactory.GetModuleInfo(GetModuleType(), "これは特殊モジュールであり、起動中は、動きを検知するとミスが記録される。!{0} status [モジュールの起動状態を確認する]");
		_needy.OnNeedyActivation += () => IRCConnection.SendMessage($"「動作検出」が起動しました。{(int) _needy.GetNeedyTimeRemaining()}秒間起動します。");

		_needy.OnTimerExpired += () => IRCConnection.SendMessage("「動作検出」が無効になりました。");
	}

	protected internal override IEnumerator RespondToCommandInternal(string inputCommand)
	{
		inputCommand = inputCommand.Trim();
		if (!inputCommand.Equals("status", StringComparison.InvariantCultureIgnoreCase))
			yield break;

		bool active = (bool) ActiveField.GetValue(_component);
		IRCConnection.SendMessage("「動作検出」は" + (active ? (int) _needy.GetNeedyTimeRemaining() + "秒間起動します。" : "停止中です。"));
	}

	private static readonly Type ComponentType = ReflectionHelper.FindType("MotionSenseModule");
	private static Component _component;
	private static readonly FieldInfo ActiveField = ComponentType.GetField("_active", BindingFlags.NonPublic | BindingFlags.Instance);
	private static readonly FieldInfo NeedyField = ComponentType.GetField("NeedyModule", BindingFlags.Public | BindingFlags.Instance);

	private readonly KMNeedyModule _needy;
}
