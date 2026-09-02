using CShellNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CShellLibTests
{
    /// <summary>
    /// The Ask family, in both of the modes it has.
    /// </summary>
    /// <remarks>
    /// Every test sets RichPrompts explicitly rather than letting it decide for itself. Left to
    /// auto it reads Console.IsInputRedirected, which is a property of whoever is running the
    /// tests -- these would then pass under one runner and pick the other mode under the next.
    /// </remarks>
    [TestClass]
    public class AskTests
    {
        private TextWriter originalOut;
        private TextReader originalIn;
        private StringWriter captured;

        [TestInitialize]
        public void Capture()
        {
            this.originalOut = Console.Out;
            this.originalIn = Console.In;
            this.captured = new StringWriter();
            Console.SetOut(this.captured);
        }

        [TestCleanup]
        public void Restore()
        {
            Console.SetOut(this.originalOut);
            Console.SetIn(this.originalIn);
        }

        private string Screen => this.captured.ToString();

        /// <summary>A shell reading typed lines.</summary>
        private static CShell Typing(params string[] lines)
        {
            Console.SetIn(new StringReader(String.Join(Environment.NewLine, lines) + Environment.NewLine));
            return new CShell { RichPrompts = false };
        }

        /// <summary>A shell reading the given keystrokes, and nothing after them.</summary>
        private static CShell Pressing(params ConsoleKeyInfo[] keys)
        {
            var queue = new Queue<ConsoleKeyInfo>(keys);
            return new CShell
            {
                RichPrompts = true,
                ReadKey = () => queue.Count > 0
                    ? queue.Dequeue()
                    : throw new InvalidOperationException("the prompt asked for more keys than the test scripted"),
            };
        }

        private static ConsoleKeyInfo Key(ConsoleKey key) => new ConsoleKeyInfo('\0', key, false, false, false);

        private static ConsoleKeyInfo Ch(char c) => new ConsoleKeyInfo(c, ConsoleKey.NoName, false, false, false);

        private static readonly ConsoleKeyInfo Enter = Key(ConsoleKey.Enter);
        private static readonly ConsoleKeyInfo Up = Key(ConsoleKey.UpArrow);
        private static readonly ConsoleKeyInfo Down = Key(ConsoleKey.DownArrow);
        private static readonly ConsoleKeyInfo Left = Key(ConsoleKey.LeftArrow);
        private static readonly ConsoleKeyInfo Right = Key(ConsoleKey.RightArrow);
        private static readonly ConsoleKeyInfo Space = new ConsoleKeyInfo(' ', ConsoleKey.Spacebar, false, false, false);

        private static readonly string[] YesNoMaybe = new[] { "yes", "no", "maybe" };

        // ------------------------------------------------------------------ AskText

        [TestMethod]
        public void AskText_ReturnsWhatWasTyped()
        {
            Assert.AreEqual("Tom", Typing("Tom").AskText("Name?"));
        }

        [TestMethod]
        public void AskText_TrimsAndAsksAsWritten()
        {
            Assert.AreEqual("Tom", Typing("   Tom   ").AskText("Name?"));
            StringAssert.Contains(this.Screen, "Name?");
        }

        [TestMethod]
        public void AskText_EmptyAnswerIsAnAnswer()
        {
            Assert.AreEqual(String.Empty, Typing("").AskText("Name?"));
        }

        [TestMethod]
        public void AskText_ThrowsAtEndOfStream()
        {
            Console.SetIn(new StringReader(String.Empty));
            var shell = new CShell { RichPrompts = false };

            var thrown = Assert.Throws<InvalidOperationException>(() => shell.AskText("Name?"));
            StringAssert.Contains(thrown.Message, "Name?");
            StringAssert.Contains(thrown.Message, "end of stream");
        }

        // ------------------------------------------------------------------ AskSecret

        [TestMethod]
        public void AskSecret_ReadsKeysWithoutEchoingThem()
        {
            var shell = Pressing(Ch('h'), Ch('u'), Ch('n'), Ch('t'), Ch('2'), Enter);

            Assert.AreEqual("hunt2", shell.AskSecret("Password?"));
            StringAssert.Contains(this.Screen, "Password?");
            Assert.IsFalse(this.Screen.Contains("hunt2"), "the secret must never reach the screen");
        }

        [TestMethod]
        public void AskSecret_BackspaceErases()
        {
            var shell = Pressing(Ch('a'), Ch('b'), Key(ConsoleKey.Backspace), Ch('c'), Enter);

            Assert.AreEqual("ac", shell.AskSecret("Password?"));
        }

        [TestMethod]
        public void AskSecret_IgnoresKeysThatCarryNoCharacter()
        {
            var shell = Pressing(Ch('a'), Key(ConsoleKey.F1), Up, Ch('b'), Enter);

            Assert.AreEqual("ab", shell.AskSecret("Password?"));
        }

        [TestMethod]
        public void AskSecret_FallsBackToALineWhenThereAreNoKeys()
        {
            // The case that matters: piped or CI input, where Console.ReadKey() would throw.
            Assert.AreEqual("sk-ant-oat01", Typing("  sk-ant-oat01  ").AskSecret("Token?"));
        }

        // ------------------------------------------------------------------ AskChoice, typed

        [TestMethod]
        public void AskChoice_TypedNumberChoosesByPosition()
        {
            Assert.AreEqual("no", Typing("2").AskChoice("Continue?", YesNoMaybe));
        }

        [TestMethod]
        public void AskChoice_TypedTextChoosesByName()
        {
            Assert.AreEqual("maybe", Typing("MAYBE").AskChoice("Continue?", YesNoMaybe));
        }

        [TestMethod]
        public void AskChoice_RejectedAnswerAsksAgain()
        {
            Assert.AreEqual("yes", Typing("nope", "1").AskChoice("Continue?", YesNoMaybe));
            StringAssert.Contains(this.Screen, "'nope' is not one of the above.");
        }

        [TestMethod]
        public void AskChoice_EmptyAnswerAsksAgain()
        {
            Assert.AreEqual("yes", Typing("", "1").AskChoice("Continue?", YesNoMaybe));
            StringAssert.Contains(this.Screen, "Pick one of the above.");
        }

        [TestMethod]
        public void AskChoice_OptionTextBeatsPositionNumber()
        {
            // The list reads 1) 3  2) 1  3) 2. Typing 3 must pick the option LABELLED 3, which
            // is the first, not the third. Position-first matching silently picked "2" here.
            var numbered = new[] { "3", "1", "2" };

            Assert.AreEqual("3", Typing("3").AskChoice("Pick:", numbered));
        }

        [TestMethod]
        public void AskChoice_LettersAreAnsweredByLetter()
        {
            Assert.AreEqual("maybe", Typing("c").AskChoice("Continue?", ChoiceStyle.Letters, YesNoMaybe));
            StringAssert.Contains(this.Screen, "c) maybe");
            StringAssert.Contains(this.Screen, "[a-c]");
        }

        [TestMethod]
        public void AskChoice_LettersDoNotAnswerToNumbers()
        {
            // Under Letters a bare 2 names nothing, so it is rejected rather than quietly
            // taken as a position.
            Assert.AreEqual("no", Typing("2", "b").AskChoice("Continue?", ChoiceStyle.Letters, YesNoMaybe));
            StringAssert.Contains(this.Screen, "'2' is not one of the above.");
        }

        [TestMethod]
        public void AskChoice_NoneIsAnsweredByTextAlone()
        {
            // Nothing is printed in front of the options, so a position number names nothing:
            // it is refused, and the prompt asks with "> " rather than advertising "[1-3]" over
            // a list with no numbers in it.
            Assert.AreEqual("no", Typing("2", "no").AskChoice("Continue?", ChoiceStyle.None, YesNoMaybe));

            StringAssert.Contains(this.Screen, "  yes");
            StringAssert.Contains(this.Screen, "> ");
            Assert.IsFalse(this.Screen.Contains("[1-3]"), "None must not ask for numbers it never showed");
            StringAssert.Contains(this.Screen, "'2' is not one of the above.");
        }

        [TestMethod]
        public void AskChoice_NoOptionsIsAMistake()
        {
            var shell = new CShell { RichPrompts = false };

            Assert.Throws<ArgumentException>(() => shell.AskChoice("Pick:", new string[0]));
        }

        [TestMethod]
        public void AskChoice_TooManyToLetterIsAMistake()
        {
            var shell = new CShell { RichPrompts = false };
            var tooMany = new string[27];
            for (int i = 0; i < tooMany.Length; i++)
            {
                tooMany[i] = "option" + i;
            }

            var thrown = Assert.Throws<ArgumentException>(
                () => shell.AskChoice("Pick:", ChoiceStyle.Letters, tooMany));
            StringAssert.Contains(thrown.Message, "26 letters");
        }

        // ------------------------------------------------------------------ ChoiceStyle.Auto

        [TestMethod]
        public void Auto_NumbersTheListWhenTheAnswerMustBeTyped()
        {
            // Without keys the label is the only thing saying what to type. A bare list under a
            // "[1-3]" prompt would leave you counting rows.
            Assert.AreEqual("no", Typing("2").AskChoice("Continue?", ChoiceStyle.Auto, YesNoMaybe));

            StringAssert.Contains(this.Screen, "1) yes");
            StringAssert.Contains(this.Screen, "[1-3]");
        }

        [TestMethod]
        public void Auto_LeavesTheListBareWhenThereAreArrowKeys()
        {
            // The selection is the affordance; a label on every row is noise.
            Assert.AreEqual("no", Pressing(Down, Enter).AskChoice("Continue?", ChoiceStyle.Auto, YesNoMaybe));

            Assert.IsFalse(this.Screen.Contains("1) yes"), "Auto should not number a list you can arrow through");
            StringAssert.Contains(this.Screen, "[no]");
        }

        [TestMethod]
        public void Auto_IsWhatTheShortOverloadPasses()
        {
            Pressing(Enter).AskChoice("Continue?", YesNoMaybe);
            Assert.IsFalse(this.Screen.Contains("1) yes"), "the short overload should be Auto, not Numbers");

            Capture();
            Typing("1").AskChoice("Continue?", YesNoMaybe);
            StringAssert.Contains(this.Screen, "1) yes");
        }

        [TestMethod]
        public void Auto_DoesNotChangeWhatATypedAnswerMeans()
        {
            // Position numbers still answer, and option text still beats them.
            Assert.AreEqual("maybe", Typing("3").AskChoice("Continue?", ChoiceStyle.Auto, YesNoMaybe));

            Capture();
            Assert.AreEqual("3", Typing("3").AskChoice("Pick:", ChoiceStyle.Auto, new[] { "3", "1", "2" }));
        }

        [TestMethod]
        public void Auto_AppliesToMultiChoiceToo()
        {
            CollectionAssert.AreEqual(
                new[] { "yes", "maybe" },
                Typing("1,3").AskMultiChoice("Pick some:", ChoiceStyle.Auto, YesNoMaybe));
            StringAssert.Contains(this.Screen, "1) yes");

            Capture();
            var picked = Pressing(Space, Enter).AskMultiChoice("Pick some:", YesNoMaybe);
            CollectionAssert.AreEqual(new[] { "yes" }, picked);
            Assert.IsFalse(this.Screen.Contains("1) "), "the short overload should be Auto here as well");
        }

        [TestMethod]
        public void None_StaysBareEvenWhenTheAnswerMustBeTyped()
        {
            // Auto is the one that adapts. None is the explicit "I really do want a bare list",
            // and answering it means naming an option.
            Assert.AreEqual("no", Typing("no").AskChoice("Continue?", ChoiceStyle.None, YesNoMaybe));

            Assert.IsFalse(this.Screen.Contains("1) yes"));
            StringAssert.Contains(this.Screen, "  yes");
        }

        // ------------------------------------------------------------------ generic options

        private record Repo(string Name, int Stars);

        private static Repo[] Repos => new[]
        {
            new Repo("cshell", 42),
            new Repo("scripts", 7),
            new Repo("crazor", 99),
        };

        [TestMethod]
        public void AskChoice_ReturnsTheOptionItselfNotItsPosition()
        {
            var chosen = Typing("scripts").AskChoice("Pick a repo:", Repos, r => r.Name);

            Assert.AreEqual("scripts", chosen.Name);
            Assert.AreEqual(7, chosen.Stars);
        }

        [TestMethod]
        public void AskChoice_LabelIsWhatIsShownAndWhatIsTyped()
        {
            // Without the selector these would be shown as "Repo { Name = ... }" and would have
            // to be answered that way too. The label governs both.
            var chosen = Typing("crazor").AskChoice("Pick a repo:", Repos, r => r.Name);

            Assert.AreEqual("crazor", chosen.Name);
            StringAssert.Contains(this.Screen, "1) cshell");
            Assert.IsFalse(this.Screen.Contains("Stars ="), "the selector should decide what is shown");
        }

        [TestMethod]
        public void AskChoice_WithoutASelectorItUsesToString()
        {
            Assert.AreEqual(7, Typing("7").AskChoice("Pick a number:", new[] { 42, 7, 99 }));
        }

        [TestMethod]
        public void AskChoice_TakesAnyEnumerableNotJustAnArray()
        {
            // A List<T> would have collapsed into a single option under a params signature.
            var names = new List<string> { "alpha", "beta" };

            Assert.AreEqual("beta", Typing("beta").AskChoice("Pick:", names));

            Capture();
            var lazy = Repos.Where(r => r.Stars > 10);
            Assert.AreEqual("crazor", Typing("crazor").AskChoice("Pick:", lazy, r => r.Name).Name);
        }

        [TestMethod]
        public void AskMultiChoice_ReturnsTheOptionsThemselves()
        {
            var chosen = Typing("cshell, crazor").AskMultiChoice("Pick repos:", Repos, r => r.Name);

            CollectionAssert.AreEqual(new[] { "cshell", "crazor" }, chosen.Select(r => r.Name).ToArray());
            Assert.AreEqual(42, chosen[0].Stars);
        }

        [TestMethod]
        public void AskChoice_NullOptionsIsAMistake()
        {
            var shell = new CShell { RichPrompts = false };

            Assert.Throws<ArgumentNullException>(() => shell.AskChoice("Pick:", (IEnumerable<string>)null));
        }

        [TestMethod]
        public void AskChoice_ANullOptionLabelsAsEmptyRatherThanThrowing()
        {
            // A hole in the list is something to see on screen, not a reason to take the prompt
            // down mid-question.
            Assert.AreEqual("b", Typing("2").AskChoice("Pick:", new[] { null, "b" }));
        }

        [TestMethod]
        public void AskChoice_NoneOffersNothingToJumpToWithAKey()
        {
            // The consequence of the rule, in the mode Auto picks when there are keys: with no
            // numbers on screen, a digit names nothing and the selection stays put. The arrow
            // keys are the way around a bare list.
            Assert.AreEqual("yes", Pressing(Ch('3'), Enter).AskChoice("Continue?", ChoiceStyle.None, YesNoMaybe));
        }

        // ------------------------------------------------------------------ AskChoice, keys

        [TestMethod]
        public void AskChoice_EnterTakesTheFirstOption()
        {
            Assert.AreEqual("yes", Pressing(Enter).AskChoice("Continue?", YesNoMaybe));
        }

        [TestMethod]
        public void AskChoice_DownArrowMovesTheSelection()
        {
            Assert.AreEqual("maybe", Pressing(Down, Down, Enter).AskChoice("Continue?", YesNoMaybe));
        }

        [TestMethod]
        public void AskChoice_SelectionWraps()
        {
            Assert.AreEqual("maybe", Pressing(Up, Enter).AskChoice("Continue?", YesNoMaybe));
            Assert.AreEqual("yes", Pressing(Down, Down, Down, Enter).AskChoice("Continue?", YesNoMaybe));
        }

        [TestMethod]
        public void AskChoice_HomeAndEndJumpToTheEnds()
        {
            Assert.AreEqual("maybe", Pressing(Key(ConsoleKey.End), Enter).AskChoice("Continue?", YesNoMaybe));
            Assert.AreEqual("yes", Pressing(Down, Key(ConsoleKey.Home), Enter).AskChoice("Continue?", YesNoMaybe));
        }

        [TestMethod]
        public void AskChoice_SelectionIsDrawnInBrackets()
        {
            Pressing(Down, Enter).AskChoice("Continue?", YesNoMaybe);

            StringAssert.Contains(this.Screen, "[no]");
        }

        [TestMethod]
        public void AskChoice_TypingAMarkerJumpsButStillWaitsForEnter()
        {
            // '3' moves to the third option; without the enter this would not return at all.
            // Explicitly Numbers: under a bare list there is no "3" on screen to jump to.
            Assert.AreEqual("maybe", Pressing(Ch('3'), Enter).AskChoice("Continue?", ChoiceStyle.Numbers, YesNoMaybe));
        }

        // ------------------------------------------------------------------ AskMultiChoice, typed

        [TestMethod]
        public void AskMultiChoice_TakesACommaSeparatedList()
        {
            CollectionAssert.AreEqual(new[] { "yes", "maybe" }, Typing("1,3").AskMultiChoice("Pick some:", YesNoMaybe));
        }

        [TestMethod]
        public void AskMultiChoice_MixesTextAndNumbers()
        {
            CollectionAssert.AreEqual(new[] { "no", "maybe" }, Typing("no, 3").AskMultiChoice("Pick some:", YesNoMaybe));
        }

        [TestMethod]
        public void AskMultiChoice_ResultIsInListOrderAndDistinct()
        {
            CollectionAssert.AreEqual(new[] { "yes", "no" }, Typing("2,1,2").AskMultiChoice("Pick some:", YesNoMaybe));
        }

        [TestMethod]
        public void AskMultiChoice_BlankChoosesNothing()
        {
            Assert.AreEqual(0, Typing("").AskMultiChoice("Pick some:", YesNoMaybe).Length);
        }

        [TestMethod]
        public void AskMultiChoice_OneBadPartRejectsTheWholeAnswer()
        {
            // Not "select the ones I understood" -- a partial selection nobody asked for is
            // worse than asking again.
            CollectionAssert.AreEqual(new[] { "yes" }, Typing("1,nope", "1").AskMultiChoice("Pick some:", YesNoMaybe));
            StringAssert.Contains(this.Screen, "'nope' is not one of the above.");
        }

        [TestMethod]
        public void AskMultiChoice_SplitsOnCommasOnlySoNamesMayHaveSpaces()
        {
            var cities = new[] { "New York", "San Jose" };

            CollectionAssert.AreEqual(new[] { "New York", "San Jose" }, Typing("New York, San Jose").AskMultiChoice("Where?", cities));
        }

        [TestMethod]
        public void AskMultiChoice_OptionTextBeatsPositionNumber()
        {
            var numbered = new[] { "3", "1", "2" };

            CollectionAssert.AreEqual(new[] { "3" }, Typing("3").AskMultiChoice("Pick some:", numbered));
        }

        [TestMethod]
        public void AskMultiChoice_LettersAreAnsweredByLetter()
        {
            CollectionAssert.AreEqual(
                new[] { "yes", "maybe" },
                Typing("a,c").AskMultiChoice("Pick some:", ChoiceStyle.Letters, YesNoMaybe));
        }

        [TestMethod]
        public void AskMultiChoice_NoOptionsIsAMistake()
        {
            var shell = new CShell { RichPrompts = false };

            Assert.Throws<ArgumentException>(() => shell.AskMultiChoice("Pick some:", new string[0]));
        }

        // ------------------------------------------------------------------ AskMultiChoice, keys

        [TestMethod]
        public void AskMultiChoice_SpaceChecksTheOptionUnderTheCursor()
        {
            var shell = Pressing(Space, Down, Down, Space, Enter);

            CollectionAssert.AreEqual(new[] { "yes", "maybe" }, shell.AskMultiChoice("Pick some:", YesNoMaybe));
        }

        [TestMethod]
        public void AskMultiChoice_SpaceTogglesBackOff()
        {
            var shell = Pressing(Space, Space, Down, Space, Enter);

            CollectionAssert.AreEqual(new[] { "no" }, shell.AskMultiChoice("Pick some:", YesNoMaybe));
        }

        [TestMethod]
        public void AskMultiChoice_EnterWithNothingCheckedChoosesNothing()
        {
            Assert.AreEqual(0, Pressing(Enter).AskMultiChoice("Pick some:", YesNoMaybe).Length);
        }

        [TestMethod]
        public void AskMultiChoice_CursorAndChecksAreDrawnSeparately()
        {
            Pressing(Space, Down, Enter).AskMultiChoice("Pick some:", YesNoMaybe);

            // The checked first option, and the cursor now resting on the unchecked second.
            StringAssert.Contains(this.Screen, "[x] yes");
            StringAssert.Contains(this.Screen, "> ");
            StringAssert.Contains(this.Screen, "[ ] no");
        }

        [TestMethod]
        public void AskMultiChoice_CursorWraps()
        {
            var shell = Pressing(Up, Space, Enter);

            CollectionAssert.AreEqual(new[] { "maybe" }, shell.AskMultiChoice("Pick some:", YesNoMaybe));
        }

        [TestMethod]
        public void AskMultiChoice_TypingAMarkerMovesTheCursorWithoutChecking()
        {
            // '3' jumps to the third option; only the space that follows checks it.
            var shell = Pressing(Ch('3'), Space, Enter);

            CollectionAssert.AreEqual(
                new[] { "maybe" },
                shell.AskMultiChoice("Pick some:", ChoiceStyle.Numbers, YesNoMaybe));
        }

        // ------------------------------------------------------------------ AskNumber, typed

        [TestMethod]
        public void AskNumber_ReturnsTheNumberTyped()
        {
            Assert.AreEqual(42, Typing("42").AskNumber("How many?"));
        }

        [TestMethod]
        public void AskNumber_UnboundedStillShowsAPrompt()
        {
            Typing("-42").AskNumber("How many?");

            // A bare cursor under a question reads as a hang rather than a prompt.
            StringAssert.Contains(this.Screen, "> ");
        }

        [TestMethod]
        public void AskNumber_OutOfRangeAsksAgain()
        {
            Assert.AreEqual(3, Typing("9", "3").AskNumber("How many?", 1, 5));
            StringAssert.Contains(this.Screen, "9 is outside 1 to 5.");
        }

        [TestMethod]
        public void AskNumber_NotANumberAsksAgain()
        {
            Assert.AreEqual(3, Typing("three", "3").AskNumber("How many?", 1, 5));
            StringAssert.Contains(this.Screen, "'three' is not a number.");
        }

        [TestMethod]
        public void AskNumber_EmptyRangeIsAMistake()
        {
            var shell = new CShell { RichPrompts = false };

            Assert.Throws<ArgumentException>(() => shell.AskNumber("How many?", 5, 1));
        }

        // ------------------------------------------------------------------ AskNumber, keys

        [TestMethod]
        public void AskNumber_ArrowsStepTheValue()
        {
            Assert.AreEqual(3, Pressing(Up, Up, Up, Enter).AskNumber("How many?", 0, 10));
            Assert.AreEqual(1, Pressing(Up, Up, Down, Enter).AskNumber("How many?", 0, 10));
        }

        [TestMethod]
        public void AskNumber_ArrowsAreHeldToTheRange()
        {
            // Starts clamped into range at 1, and cannot be stepped below it.
            Assert.AreEqual(1, Pressing(Down, Down, Down, Enter).AskNumber("How many?", 1, 5));
            Assert.AreEqual(5, Pressing(Up, Up, Up, Up, Up, Up, Up, Enter).AskNumber("How many?", 1, 5));
        }

        [TestMethod]
        public void AskNumber_DigitsAreTyped()
        {
            Assert.AreEqual(12, Pressing(Key(ConsoleKey.Backspace), Ch('1'), Ch('2'), Enter).AskNumber("How many?", 0, 99));
        }

        [TestMethod]
        public void AskNumber_EnterIsRefusedWhileTheValueIsOutOfRange()
        {
            // 7 is outside 1-5, so the first enter does nothing; backspace then 4 makes it valid.
            var shell = Pressing(Ch('7'), Enter, Key(ConsoleKey.Backspace), Key(ConsoleKey.Backspace), Ch('4'), Enter);

            Assert.AreEqual(4, shell.AskNumber("How many?", 1, 5));
        }

        // ------------------------------------------------------------------ AskYesNo, typed

        [TestMethod]
        public void AskYesNo_AnswersYesAndNo()
        {
            Assert.IsTrue(Typing("y").AskYesNo("Sure?"));
            Assert.IsTrue(Typing("YES").AskYesNo("Sure?"));
            Assert.IsFalse(Typing("n").AskYesNo("Sure?"));
            Assert.IsFalse(Typing("No").AskYesNo("Sure?"));
        }

        [TestMethod]
        public void AskYesNo_EnterTakesTheDefault()
        {
            Assert.IsFalse(Typing("").AskYesNo("Push to main?", false));
            Assert.IsTrue(Typing("").AskYesNo("Keep the backup?", true));
        }

        [TestMethod]
        public void AskYesNo_ShowsTheDefaultCapitalised()
        {
            Typing("").AskYesNo("Push to main?", false);
            StringAssert.Contains(this.Screen, "[y/N]");

            Capture();
            Typing("").AskYesNo("Keep the backup?", true);
            StringAssert.Contains(this.Screen, "[Y/n]");
        }

        [TestMethod]
        public void AskYesNo_WithNoDefaultEnterAsksAgain()
        {
            Assert.IsTrue(Typing("", "y").AskYesNo("Sure?"));
            StringAssert.Contains(this.Screen, "[y/n]");
            StringAssert.Contains(this.Screen, "Answer y or n.");
        }

        // ------------------------------------------------------------------ AskYesNo, keys

        [TestMethod]
        public void AskYesNo_KeysMoveTheSelectionAndEnterTakesIt()
        {
            Assert.IsTrue(Pressing(Ch('y'), Enter).AskYesNo("Sure?"));
            Assert.IsFalse(Pressing(Ch('n'), Enter).AskYesNo("Sure?"));
        }

        [TestMethod]
        public void AskYesNo_AKeyOnItsOwnDoesNotAnswer()
        {
            // Pressing() throws once the scripted keys run out, which is only reachable if 'y'
            // did not answer on its own. One keystroke is never enough to answer a question.
            var thrown = Assert.Throws<InvalidOperationException>(() => Pressing(Ch('y')).AskYesNo("Sure?"));

            StringAssert.Contains(thrown.Message, "more keys than the test scripted");
        }

        [TestMethod]
        public void AskYesNo_KeysCanBeChangedBeforeEnter()
        {
            // Reached for y, thought better of it. Nothing was committed on the way.
            Assert.IsFalse(Pressing(Ch('y'), Ch('n'), Enter).AskYesNo("Delete everything?", false));
        }

        [TestMethod]
        public void AskYesNo_ArrowsMoveBetweenThem()
        {
            Assert.IsFalse(Pressing(Right, Enter).AskYesNo("Push to main?", true));
            Assert.IsTrue(Pressing(Left, Enter).AskYesNo("Push to main?", false));
        }

        [TestMethod]
        public void AskYesNo_EnterTakesTheSelectedSide()
        {
            Assert.IsTrue(Pressing(Enter).AskYesNo("Keep the backup?", true));
            Assert.IsFalse(Pressing(Enter).AskYesNo("Push to main?", false));
        }

        [TestMethod]
        public void AskYesNo_SelectionIsDrawnInBrackets()
        {
            Pressing(Enter).AskYesNo("Push to main?", false);

            StringAssert.Contains(this.Screen, "[No]");
        }

        [TestMethod]
        public void AskYesNo_IgnoresKeysThatMeanNothing()
        {
            Assert.IsTrue(Pressing(Key(ConsoleKey.F1), Ch('q'), Enter).AskYesNo("Sure?", true));
        }

        // ------------------------------------------------------------------ the rich-path gaps

        [TestMethod]
        public void AskNumber_MinusSignTypesANegative()
        {
            // The value starts at 0, so the minus has to follow a backspace. askdemo tells people
            // to try this, and nothing was checking it worked.
            var shell = Pressing(Key(ConsoleKey.Backspace), Ch('-'), Ch('4'), Ch('2'), Enter);

            Assert.AreEqual(-42, shell.AskNumber("Any whole number?"));
        }

        [TestMethod]
        public void AskNumber_MinusSignOnlyLeads()
        {
            // '4', then '-', then '2'. The minus arrives with digits already typed and is
            // dropped rather than landing in the middle of the number.
            var shell = Pressing(Ch('4'), Ch('-'), Ch('2'), Enter);

            Assert.AreEqual(42, shell.AskNumber("Any whole number?"));
        }

        [TestMethod]
        public void AskNumber_UnboundedTakesKeysAndShowsNoRange()
        {
            Assert.AreEqual(2, Pressing(Up, Up, Enter).AskNumber("Any whole number?"));

            StringAssert.Contains(this.Screen, "Any whole number?");
            Assert.IsFalse(this.Screen.Contains("["), "an unbounded ask has no range to advertise");
        }

        [TestMethod]
        public void AskNumber_UnboundedArrowsGoNegative()
        {
            Assert.AreEqual(-2, Pressing(Down, Down, Enter).AskNumber("Any whole number?"));
        }

        [TestMethod]
        public void AskMultiChoice_HomeAndEndJumpToTheEnds()
        {
            var shell = Pressing(Key(ConsoleKey.End), Space, Key(ConsoleKey.Home), Space, Enter);

            CollectionAssert.AreEqual(new[] { "yes", "maybe" }, shell.AskMultiChoice("Pick some:", YesNoMaybe));
        }

        [TestMethod]
        public void AskYesNo_TabMovesBetweenThemToo()
        {
            Assert.IsFalse(Pressing(Key(ConsoleKey.Tab), Enter).AskYesNo("Push to main?", true));
            Assert.IsTrue(Pressing(Key(ConsoleKey.Tab), Enter).AskYesNo("Push to main?", false));
        }

        [TestMethod]
        public void AskChoice_LettersJumpByLetterWhenThereAreKeys()
        {
            Assert.AreEqual("maybe", Pressing(Ch('c'), Enter).AskChoice("Continue?", ChoiceStyle.Letters, YesNoMaybe));

            // An explicit style labels the list even in the mode Auto would have left bare.
            StringAssert.Contains(this.Screen, "c) ");
        }

        [TestMethod]
        public void AskMultiChoice_TakesAStyleWhenThereAreKeys()
        {
            var shell = Pressing(Ch('b'), Space, Enter);

            CollectionAssert.AreEqual(
                new[] { "no" },
                shell.AskMultiChoice("Pick some:", ChoiceStyle.Letters, YesNoMaybe));
            StringAssert.Contains(this.Screen, "b) ");
        }

        [TestMethod]
        public void AskSecret_TrimsWhatWasTypedToo()
        {
            // The line-reading fallback trims; so does the key path, so a pasted token with a
            // stray space either side behaves the same whichever mode caught it.
            var shell = Pressing(Ch(' '), Ch('a'), Ch('b'), Ch(' '), Enter);

            Assert.AreEqual("ab", shell.AskSecret("Token?"));
        }

        [TestMethod]
        public void AskChoice_ASelectorReturningNullLabelsAsEmpty()
        {
            // Blank rows on screen, but still answerable by position, and no exception out of
            // the middle of a prompt.
            Assert.AreEqual(2, Typing("2").AskChoice("Pick:", new[] { 1, 2 }, x => null));
        }

        [TestMethod]
        public void AskMultiChoice_SaysItsOwnNameWhenTheOptionsAreImpossible()
        {
            // Shared validation, but the message names the method the caller actually called.
            var shell = new CShell { RichPrompts = false };

            var nothing = Assert.Throws<ArgumentNullException>(
                () => shell.AskMultiChoice("Pick some:", (IEnumerable<string>)null));
            StringAssert.Contains(nothing.Message, "AskMultiChoice");

            var tooMany = new string[27];
            for (int i = 0; i < tooMany.Length; i++)
            {
                tooMany[i] = "option" + i;
            }

            var lettered = Assert.Throws<ArgumentException>(
                () => shell.AskMultiChoice("Pick some:", ChoiceStyle.Letters, tooMany));
            StringAssert.Contains(lettered.Message, "AskMultiChoice");
            StringAssert.Contains(lettered.Message, "26 letters");
        }
    }
}
