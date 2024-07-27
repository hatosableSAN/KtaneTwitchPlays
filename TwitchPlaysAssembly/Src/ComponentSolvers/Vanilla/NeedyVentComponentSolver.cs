using System.Collections;

public class NeedyVentComponentSolver : ComponentSolver
{
	public NeedyVentComponentSolver(TwitchModule module) :
		base(module)
	{
		var ventModule = (NeedyVentComponent) module.BombComponent;
		_yesButton = ventModule.YesButton;
		_noButton = ventModule.NoButton;
		ModInfo = ComponentSolverFactory.GetModuleInfo("NeedyVentGas", "!{0} yes, !{0} y [Yを押す] | !{0} no, !{0} n [Nを押す]");
		ModInfo.moduleTranslatedName = "ガス放出";
		ModInfo.manualCode = "https://ktane.timwi.de/HTML/Venting%20Gas%20translated%20(%E6%97%A5%E6%9C%AC%E8%AA%9E%20%E2%80%94%20%E3%82%AC%E3%82%B9%E6%94%BE%E5%87%BA).html";
	}

	protected internal override IEnumerator RespondToCommandInternal(string inputCommand)
	{
		inputCommand = inputCommand.ToLowerInvariant().Trim();
		if (inputCommand.EqualsAny("y", "yes", "press y", "press yes"))
		{
			yield return "yes";
			yield return DoInteractionClick(_yesButton);
		}
		else if (inputCommand.EqualsAny("n", "no", "press n", "press no"))
		{
			yield return "no";
			yield return DoInteractionClick(_noButton);
		}
	}

	private readonly KeypadButton _yesButton;
	private readonly KeypadButton _noButton;
}
