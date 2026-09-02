#!/usr/bin/env dotnet-script
#r "nuget: MedallionShell, 1.6.2"
#r "src/bin/Debug/netstandard2.0/CShell.dll"

using CShellNet;
using static CShellNet.Globals;

// askdemo -- a quick tour of every AskXXX() method. Each one shows you the code, then
// runs exactly that code so you can answer it.
//
//   dotnet script askdemo.csx              arrow keys, if you're at a terminal
//   dotnet script askdemo.csx -- -plain    typed answers instead
//   echo ... | dotnet script askdemo.csx   typed, because it has no choice
//
// Build the library first:  dotnet build src

if (Args.Any(a => a is "-h" or "-?" or "--help"))
{
    Console.WriteLine("askdemo [-plain|-rich]");
    Console.WriteLine("  A tour of AskText, AskSecret, AskYesNo, AskNumber,");
    Console.WriteLine("  AskChoice and AskMultiChoice.");
    Console.WriteLine();
    Console.WriteLine("  -plain  typed answers");
    Console.WriteLine("  -rich   arrow keys");
    return;
}

if (Args.Any(a => a is "-plain" or "--plain")) RichPrompts = false;
if (Args.Any(a => a is "-rich" or "--rich")) RichPrompts = true;

var rich = RichPrompts ?? !Console.IsInputRedirected;

// Strip the leading indentation the verbatim snippets below carry, keeping the relative
// indent inside a snippet so a wrapped argument still lines up.
string[] Dedent(string text)
{
    var lines = text.Replace("\r\n", "\n").Split('\n')
                    .SkipWhile(l => l.Trim().Length == 0).ToList();
    while (lines.Count > 0 && lines[lines.Count - 1].Trim().Length == 0)
    {
        lines.RemoveAt(lines.Count - 1);
    }

    var indent = lines.Where(l => l.Trim().Length > 0)
                      .Select(l => l.Length - l.TrimStart().Length)
                      .DefaultIfEmpty(0).Min();

    return lines.Select(l => l.Length >= indent ? l.Substring(indent) : l.Trim()).ToArray();
}

// The code you're about to run, in a box. Every section below keeps the box and the real
// call next to each other, so the two can't quietly drift apart.
void Box(string code)
{
    var lines = Dedent(code);
    var width = Math.Max(68, lines.Max(l => l.Length));

    Console.WriteLine("   ┌─" + new string('─', width) + "─┐");
    foreach (var line in lines)
    {
        Console.WriteLine("   │ " + line.PadRight(width) + " │");
    }

    Console.WriteLine("   └─" + new string('─', width) + "─┘");
}

void Lesson(string title, string description, string code, string richHint, string plainHint)
{
    Console.WriteLine();
    Console.WriteLine("==== " + title + " " + new string('=', Math.Max(4, 78 - title.Length - 6)));
    foreach (var line in description.Replace("\r\n", "\n").Split('\n'))
    {
        Console.WriteLine("   " + line.Trim());
    }

    Console.WriteLine();
    Box(code);
    Console.WriteLine();
    Console.WriteLine("   TRY: " + (rich ? richHint : plainHint));
    Console.WriteLine();
}

Console.WriteLine("══ The Ask Methods ═══════════════════════════════════════════════════════════");
Console.WriteLine();
Console.WriteLine("  The Ask() methods are prompts for asking whoever's running your script a question.");
Console.WriteLine();
Console.WriteLine();

// Guarded on IsInputRedirected rather than on `rich`, because that is the thing ReadKey()
// actually needs. Piping answers in would otherwise eat one of them here.
if (!Console.IsInputRedirected)
{
    Console.Write("  Hit any key to start.");
    Console.ReadKey(intercept: true);
    Console.WriteLine();
    Console.WriteLine();
}

// If the input runs out we stop here, naming the question nobody answered.
try
{
    // ---------------------------------------------------------------- AskText

    Lesson("AskText(string question) -> string",
        @"Grab a line of text. Whatever they type, trimmed.
          Blank counts as an answer, so check for it if you care.",
        @"var name = AskText(""What should I call you?"");",
        "type a name and hit enter.",
        "type a name and hit enter.");

    var name = AskText("What should I call you?");
    Console.WriteLine($"  -> \"{name}\"{(name.Length == 0 ? "  (nothing is a valid answer)" : "")}");

    // ---------------------------------------------------------------- AskSecret

    Lesson("AskSecret(string question) -> string",
        @"Same, but nothing appears as they type -- for tokens and passwords you'd
          rather not leave sitting on the screen. Backspace still works.
          If input is piped it just reads a line; there's no screen to leak onto.",
        @"var secret = AskSecret(""Paste a token (nothing will appear):"");",
        "type something. You won't see it. Enter when you're done.",
        "type or paste a value and hit enter.");

    var secret = AskSecret("Paste a token (nothing will appear):");
    Console.WriteLine(secret.Length > 0
        ? $"  -> {secret.Length} characters, starting {secret.Substring(0, Math.Min(4, secret.Length))}...  (never printed in full)"
        : "  -> nothing entered");

    // ---------------------------------------------------------------- AskYesNo

    Lesson("AskYesNo(string question) -> bool",
        @"A yes/no question.
          Enter is what answers. y and n just move the highlight, so a stray
          keypress can't commit you to anything.",
        @"var sure = AskYesNo(""Ready to see the rest?"");",
        "left/right, tab, or y/n to move. Enter to answer.",
        "type y, yes, n or no. Enter on its own just asks again.");

    var sure = AskYesNo("Ready to see the rest?");
    Console.WriteLine($"  -> {sure}");

    Lesson("AskYesNo(string question, bool defaultAnswer) -> bool",
        @"Pass a default and enter takes it.
          The capital in [y/N] tells you which one that is. Make it the safe
          answer -- enter is what people press without reading.",
        @"
          var push   = AskYesNo(""Push straight to main?"", false);
          var backup = AskYesNo(""Keep a backup first?"", true);",
        "hit enter for the default, or move off it first.",
        "hit enter for the default, or type y/n to override.");

    var push = AskYesNo("Push straight to main?", false);
    Console.WriteLine($"  -> {push}   (enter would have meant No)");

    var backup = AskYesNo("Keep a backup first?", true);
    Console.WriteLine($"  -> {backup}   (enter would have meant Yes)");

    // ---------------------------------------------------------------- AskNumber

    Lesson("AskNumber(string question, int min, int max) -> int",
        @"A whole number, kept inside the range you give it.
          Arrows nudge it up and down, digits type it. It won't let the value
          wander outside min..max, so you can't be handed one you'd refuse.",
        @"var retries = AskNumber(""How many retries?"", 1, 5);",
        "up/down to step, or type digits. Backspace edits. Enter accepts.",
        "type a number from 1 to 5. Try 9 first and watch it say no.");

    var retries = AskNumber("How many retries?", 1, 5);
    Console.WriteLine($"  -> {retries}");

    Lesson("AskNumber(string question) -> int",
        @"Same thing without a range. Any whole number, negatives included.",
        @"var anything = AskNumber(""Any whole number at all?"");",
        "arrows and digits, same as before. Try a minus sign.",
        "type any whole number.");

    var anything = AskNumber("Any whole number at all?");
    Console.WriteLine($"  -> {anything}");

    // ---------------------------------------------------------------- AskChoice

    Lesson("AskChoice<T>(string question, IEnumerable<T> options) -> T",
        @"Pick one from a list. You get the option itself back, not its position.
          Anything enumerable will do -- an array, a List, a LINQ query.",
        @"
          string[] fruits = [""apple"", ""banana"", ""cherry""];

          var fruit = AskChoice(""Pick a fruit:"", fruits);",
        "up/down to move (it wraps), home/end to jump, enter to pick.",
        "type the number, or the option itself -- 'banana' works as well as 2.");

    string[] fruits = ["apple", "banana", "cherry"];

    var fruit = AskChoice("Pick a fruit:", fruits);
    Console.WriteLine($"  -> {fruit}");

    Lesson("AskChoice<T>(..., ChoiceStyle style, ...) -> T",
        @"ChoiceStyle sets the labels: Auto, Numbers, Letters or None.
          Auto is the default -- no labels when there are arrow keys, numbers when
          the answer has to be typed. Letters also changes what they can type:
          'b' picks the second one, and a bare '2' means nothing.",
        @"
          var lettered = AskChoice(""Pick again, by letter:"",
                                   ChoiceStyle.Letters, fruits);",
        "arrows as before. Typing 'c' jumps there, but enter still picks.",
        "type a, b or c. Try 2 first and watch it bounce.");

    var lettered = AskChoice("Pick again, by letter:", ChoiceStyle.Letters, fruits);
    Console.WriteLine($"  -> {lettered}");

    Lesson("ChoiceStyle.None",
        @"None puts nothing in front of the options. With nothing on screen to
          reference, you answer with the option's own text -- a number would be
          naming something the list never showed.",
        @"
          var colour = AskChoice(""Pick a colour:"", ChoiceStyle.None,
                                  [""red"", ""green"", ""blue""]);",
        "arrows and enter, as ever.",
        "type the colour itself -- 'green'. A number gets bounced.");

    var colour = AskChoice("Pick a colour:", ChoiceStyle.None, ["red", "green", "blue"]);
    Console.WriteLine($"  -> {colour}");

    Lesson("AskChoice<T>(..., Func<T,string> label) -> T",
        @"Options don't have to be strings. Hand it your own objects and a selector
          saying what to show for each, and you get the object back -- no lookup.
          The label is also what they type, so they answer with what they can see.",
        @"
          var repos = new[]
          {
              (Name: ""cshell"", Stars: 42),
              (Name: ""scripts"", Stars: 7),
              (Name: ""crazor"", Stars: 99),
          };

          var repo = AskChoice(""Pick a repo:"", repos, r => r.Name);",
        "arrows and enter, same as always.",
        "type a repo name, or its number.");

    var repos = new[]
    {
        (Name: "cshell", Stars: 42),
        (Name: "scripts", Stars: 7),
        (Name: "crazor", Stars: 99),
    };

    var repo = AskChoice("Pick a repo:", repos, r => r.Name);
    Console.WriteLine($"  -> {repo.Name}, which has {repo.Stars} stars  (a whole tuple back, not an index)");

    // ---------------------------------------------------------------- AskMultiChoice

    Lesson("AskMultiChoice<T>(string question, IEnumerable<T> options) -> T[]",
        @"Pick as many as you like. You get the options themselves back.
          The > shows where you are, [x] shows what's checked -- two marks, because
          they're two different things.
          Picking nothing is a real answer: you get an empty array rather than being
          asked again. If you need at least one, say so yourself, like the loop below.",
        @"
          string[] toppings = [""cheese"", ""tomato"", ""basil"", ""olives""];

          string[] chosen;
          do
          {
              chosen = AskMultiChoice(""Choose your toppings:"", toppings);
          }
          while (chosen.Length == 0);",
        "up/down to move, SPACE to check, enter when done. Try enter with nothing checked.",
        "a comma separated list: '1,3' or 'cheese, basil'. Only commas split, so names can have spaces.");

    string[] toppings = ["cheese", "tomato", "basil", "olives"];

    string[] chosen;
    do
    {
        chosen = AskMultiChoice("Choose your toppings:", toppings);
        if (chosen.Length == 0)
        {
            Console.WriteLine("  -> nothing chosen, which is allowed -- this demo is the one");
            Console.WriteLine("     asking for at least one. Go again.");
        }
    }
    while (chosen.Length == 0);

    Console.WriteLine($"  -> {string.Join(", ", chosen)}");

    // ---------------------------------------------------------------- summary

    Console.WriteLine();
    Console.WriteLine("══ What you said ════════════════════════════════════════════════════════════");
    Console.WriteLine();
    Console.WriteLine($"  AskText         {name}");
    Console.WriteLine($"  AskSecret       {secret.Length} characters (never printed)");
    Console.WriteLine($"  AskYesNo        {sure}");
    Console.WriteLine($"  AskYesNo(false) {push}");
    Console.WriteLine($"  AskYesNo(true)  {backup}");
    Console.WriteLine($"  AskNumber(1,5)  {retries}");
    Console.WriteLine($"  AskNumber       {anything}");
    Console.WriteLine($"  AskChoice       {fruit}");
    Console.WriteLine($"  ..Letters       {lettered}");
    Console.WriteLine($"  ..None          {colour}");
    Console.WriteLine($"  ..selector      {repo.Name}");
    Console.WriteLine($"  AskMultiChoice  {string.Join(", ", chosen)}");
    Console.WriteLine();
}
catch (InvalidOperationException e)
{
    // The input ran out -- a pipe with too few lines in it, most likely.
    Console.WriteLine();
    Console.WriteLine(e.Message);
    Environment.Exit(1);
}
