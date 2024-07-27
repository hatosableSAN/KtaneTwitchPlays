using System;
using System.Collections;
using System.Text.RegularExpressions;
using Assets.Scripts.Components.VennWire;
using Assets.Scripts.Rules;

public class VennWireComponentSolver : ComponentSolver
{
	public VennWireComponentSolver(TwitchModule module) :
		base(module)
	{
		_wires = ((VennWireComponent) module.BombComponent).ActiveWires;
		ModInfo = ComponentSolverFactory.GetModuleInfo("Venn", "!{0} cut 3 [ワイヤ3を切る] | !{0} cut 2 3 6 [複数のワイヤを切る] | ワイヤは左から順に数える | ワイヤのないプラグは数えない");
		ModInfo.moduleTranslatedName = "複雑ワイヤ";
		ModInfo.manualCode = "https://ktane.timwi.de/HTML/Complicated%20Wires%20translated%20(%E6%97%A5%E6%9C%AC%E8%AA%9E%20%E2%80%94%20%E8%A4%87%E9%9B%91%E3%83%AF%E3%82%A4%E3%83%A4).html";
	}

	protected internal override IEnumerator RespondToCommandInternal(string inputCommand)
	{
		inputCommand = inputCommand.Trim();
		if (!inputCommand.StartsWith("cut ", StringComparison.InvariantCultureIgnoreCase))
		{
			yield break;
		}
		inputCommand = inputCommand.Substring(4);

		foreach (Match wireIndexString in Regex.Matches(inputCommand, @"[1-6]"))
		{
			if (!int.TryParse(wireIndexString.Value, out int wireIndex))
			{
				continue;
			}
			wireIndex--;

			if (wireIndex < 0 || wireIndex >= _wires.Length) continue;
			if (_wires[wireIndex].Snipped)
				continue;

			yield return wireIndexString.Value;

			yield return "trycancel";
			VennSnippableWire wire = _wires[wireIndex];
			yield return DoInteractionClick(wire, $"cutting wire {wireIndexString.Value}");
		}
	}

	protected override IEnumerator ForcedSolveIEnumerator()
	{
		yield return null;
		VennWireComponent vwc = (VennWireComponent) Module.BombComponent;
		VennWireRuleSet ruleSet = RuleManager.Instance.VennWireRuleSet;
		foreach (VennSnippableWire wire in _wires)
		{
			if (ruleSet.ShouldWireBeSnipped(vwc, wire.WireIndex, false) && !wire.Snipped)
				yield return DoInteractionClick(wire);
		}
	}

	private readonly VennSnippableWire[] _wires;
}
