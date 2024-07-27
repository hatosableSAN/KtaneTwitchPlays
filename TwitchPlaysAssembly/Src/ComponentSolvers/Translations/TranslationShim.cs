using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

public class TranslationShim : ComponentSolverShim
{

	private readonly TranslationBuilder _inputTranslation;
	private readonly TranslationBuilder _outputTranslation;

	public TranslationShim(TwitchModule module, TranslationBuilder inputTranslation, TranslationBuilder outputTranslation) : base(module)
	{
		_inputTranslation = inputTranslation ?? Build();
		_outputTranslation = outputTranslation ?? Build();
	}

	private string ApplyTranslation(string input, List<Tuple<Regex, string>> translation)
	{
		return translation.Aggregate(input, (acc, next) => next.First.Replace(acc, next.Second));
	}


	protected override IEnumerator RespondToCommandShimmed(string inputCommand)
	{
		IEnumerator respondToCommandInternal = RespondToCommandUnshimmed(_inputTranslation.ApplyTranlation(inputCommand));
		while (respondToCommandInternal.MoveNext())
		{
			if(respondToCommandInternal.Current is string)
			{
				yield return _outputTranslation.ApplyTranlation((string) respondToCommandInternal.Current);
			} else
			{
				yield return respondToCommandInternal.Current;
			}
		}
	}

	public static TranslationBuilder Build() => new TranslationBuilder();
	public class TranslationBuilder
	{
		List<Tuple<Regex, string>> _rlist;
		List<Tuple<string, string>> _slist;

		internal TranslationBuilder()
		{
			_rlist = new List<Tuple<Regex, string>>();
			_slist = new List<Tuple<string, string>>();
		}
		internal TranslationBuilder Add(string from, string to)
		{
			_slist.Add(new Tuple<string, string>(from, to));
			return this;
		}
		internal TranslationBuilder Add(Regex from, string to)
		{
			_rlist.Add(new Tuple<Regex, string>(from, to));
			return this;
		}

		internal string ApplyTranlation(string input)
		{
			var rres = _rlist.Aggregate(input, (acc, next) => next.First.Replace(acc, next.Second));
			return _slist.Aggregate(rres, (acc, next) => Regex.Replace(acc, next.First, next.Second));
		}
	}
}
