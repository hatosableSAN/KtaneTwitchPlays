using System;
using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Rules;

public class WireSetComponentSolver : ComponentSolver
{
	public WireSetComponentSolver(TwitchModule module) :
		base(module)
	{
		_wires = ((WireSetComponent) module.BombComponent).wires;
		ModInfo = ComponentSolverFactory.GetModuleInfo("Wires", "!{0} cut 3 [ワイヤ3を切る] | ワイヤは上から順に数える | 空いているプラグは数えない");
		ModInfo.moduleTranslatedName = "ワイヤ";
		ModInfo.manualCode = "https://ktane.timwi.de/HTML/Wires%20translated%20(%E6%97%A5%E6%9C%AC%E8%AA%9E%20%E2%80%94%20%E3%83%AF%E3%82%A4%E3%83%A4).html";
	}

	protected internal override IEnumerator RespondToCommandInternal(string inputCommand)
	{
		inputCommand = inputCommand.Trim();
		if (!inputCommand.StartsWith("cut ", StringComparison.InvariantCultureIgnoreCase))
			yield break;
		inputCommand = inputCommand.Substring(4);

		if (!int.TryParse(inputCommand, out int wireIndex) || wireIndex < 1 || wireIndex > _wires.Count) yield break;

		yield return null;
		yield return DoInteractionClick(_wires[wireIndex - 1]);
	}

	protected override IEnumerator ForcedSolveIEnumerator()
	{
		yield return null;
		yield return DoInteractionClick(_wires[RuleManager.Instance.WireRuleSet.GetSolutionIndex((WireSetComponent) Module.BombComponent)]);
	}

	private readonly List<SnippableWire> _wires;
}
