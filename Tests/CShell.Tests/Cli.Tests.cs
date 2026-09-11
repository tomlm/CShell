using CShellNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CShellLibTests
{
    /// <summary>
    /// Cli -- what a script declares it accepts, and how a command line is read against it.
    /// </summary>
    /// <remarks>
    /// Every test names the program explicitly. Left alone, Cli.For() takes the name from the
    /// calling file via [CallerFilePath], which here would be this test file -- so assertions
    /// about messages would be asserting on "Cli.Tests".
    /// </remarks>
    [TestClass]
    public class CliTests
    {
        private TextWriter originalOut;
        private TextWriter originalError;
        private StringWriter captured;
        private StringWriter capturedErrors;

        [TestInitialize]
        public void Capture()
        {
            this.originalOut = Console.Out;
            this.originalError = Console.Error;
            this.captured = new StringWriter();
            this.capturedErrors = new StringWriter();
            Console.SetOut(this.captured);
            Console.SetError(this.capturedErrors);
        }

        [TestCleanup]
        public void Restore()
        {
            Console.SetOut(this.originalOut);
            Console.SetError(this.originalError);
        }

        private string Screen => this.captured.ToString();

        private string Errors => this.capturedErrors.ToString();

        private static Cli Given(params string[] args) => Cli.For(args).Program("demo");

        // ------------------------------------------------------------------ switches

        [TestMethod]
        public void Switch_IsTrueWhenGivenAndFalseWhenAbsent()
        {
            Assert.IsTrue(Given("-whatif").Switch("whatif", "touch nothing").TryParse().Switch("whatif"));
            Assert.IsFalse(Given().Switch("whatif", "touch nothing").TryParse().Switch("whatif"));
        }

        [TestMethod]
        public void Switch_AcceptsEitherDashPrefix()
        {
            foreach (var spelling in new[] { "-whatif", "--whatif" })
            {
                Assert.IsTrue(Given(spelling).Switch("whatif", "touch nothing").TryParse().Switch("whatif"), spelling);
            }
        }

        [TestMethod]
        public void Switch_IgnoresCaseHyphensAndUnderscores()
        {
            foreach (var spelling in new[] { "--DRY-RUN", "--dryrun", "-Dry_Run", "--d-r-y-r-u-n" })
            {
                Assert.IsTrue(Given(spelling).Switch("dry-run", "print only").TryParse().Switch("dry-run"), spelling);
            }
        }

        [TestMethod]
        public void Switch_AliasesAfterThePipeSetTheSameSwitch()
        {
            foreach (var spelling in new[] { "-whatif", "--dry-run", "-n" })
            {
                Assert.IsTrue(Given(spelling).Switch("whatif|dry-run|n", "touch nothing").TryParse().Switch("whatif"), spelling);
            }

            // and it reads back under any of its names
            var cmd = Given("-n").Switch("whatif|dry-run|n", "touch nothing").TryParse();
            Assert.IsTrue(cmd.Switch("dry-run"));
        }

        [TestMethod]
        public void Switch_RepeatedIsStillJustTrue()
        {
            Assert.IsTrue(Given("-whatif", "--whatif").Switch("whatif", "touch nothing").TryParse().Switch("whatif"));
        }

        [TestMethod]
        public void Switch_GivenAValueIsAnError()
        {
            var cmd = Given("-whatif:true").Switch("whatif", "touch nothing").TryParse();

            Assert.IsTrue(cmd.ShouldExit);
            Assert.AreEqual(1, cmd.ExitCode);
            StringAssert.Contains(this.Errors, "takes no value");
        }

        [TestMethod]
        public void Switch_ReadingAnUndeclaredNameThrowsAndSaysWhatWasDeclared()
        {
            var cmd = Given().Switch("whatif", "touch nothing").TryParse();

            var thrown = Assert.Throws<ArgumentException>(() => cmd.Switch("nopush"));
            StringAssert.Contains(thrown.Message, "nopush");
            StringAssert.Contains(thrown.Message, "whatif");
        }

        // ------------------------------------------------------------------ typed declarations

        public enum Format { Json, Xml, Yaml }

        public enum Color { Red, Green, Blue }

        [Flags]
        public enum Trace { None = 0, Reads = 1, Writes = 2 }

        [TestMethod]
        public void Typed_TheVariableNamesTheSwitch()
        {
            Given("--queue-length:8").Option(out int queueLength, "how many to queue").TryParse();

            Assert.AreEqual(8, queueLength);
        }

        [TestMethod]
        public void Typed_CamelCaseBecomesKebabInTheHelpAndStillMatchesEverySpelling()
        {
            foreach (var spelling in new[] { "--queue-length:8", "--queuelength:8", "-Queue_Length:8", "-QUEUELENGTH:8" })
            {
                Given(spelling).Option(out int queueLength, "how many to queue").TryParse();
                Assert.AreEqual(8, queueLength, spelling);
            }
        }

        [TestMethod]
        public void Typed_TheHelpShowsTheNameTheWayItWasSpelledOut()
        {
            var usage = Given().Option(out int queueLength, "how many to queue").TryParse().UsageText;

            StringAssert.Contains(usage, "--queue-length:<value>");
        }

        [TestMethod]
        public void Typed_TheNameIsSplitOnTheHumpsAndRunsOfCapitalsStayTogether()
        {
            // expr is normally filled in by the compiler; passing it is how the shapes it can hand
            // over get covered without a variable of each name.
            var expected = new Dictionary<string, string>
            {
                { "out int queueLength", "--queue-length" },
                { "out var apiKey",      "--api-key" },
                { "out string APIKey",   "--api-key" },
                { "out int maxCPU",      "--max-cpu" },
                { "out string[] files",  "--files" },
                { "out int? port",       "--port" },
                { "out string file",     "--file" },
                { "out int queue_length","--queue-length" },
                { "out int top10",       "--top10" },
                { "out this.retries",    "--retries" },
                { "out config.Retries",  "--retries" },
                { "out @out",            "--out" },
                { "  file  ",            "--file" },
            };

            foreach (var pair in expected)
            {
                var usage = Given().Option(out string ignored, "where to write it", null, pair.Key).TryParse().UsageText;
                StringAssert.Contains(usage, pair.Value + ":<value>", pair.Key);
            }
        }

        [TestMethod]
        public void Typed_AnExpressionWithNoNameInItThrowsAndSaysWhatToDoInstead()
        {
            foreach (var nameless in new[] { "out _", "out list[0]", "out (a, b)", null, "   " })
            {
                var thrown = Assert.Throws<ArgumentException>(
                    () => Given().Option(out string ignored, "where to write it", null, nameless));

                StringAssert.Contains(thrown.Message, "Option(name, help)");
            }
        }

        [TestMethod]
        public void Typed_AbsentLeavesTheDefaultAndNullableTellsThatFromZero()
        {
            Given().Option(out int port, "the port to listen on")
                   .Option(out int? explicitPort, "the port to listen on").TryParse();

            Assert.AreEqual(0, port);
            Assert.IsNull(explicitPort);

            Given("--explicit-port:0").Option(out int? given, "the port to listen on", "explicit-port").TryParse();
            Assert.AreEqual(0, given.Value);
        }

        [TestMethod]
        public void Typed_SwitchReadsIntoABool()
        {
            Given("--dry-run").Switch(out bool dryRun, "print, do not do").TryParse();
            Assert.IsTrue(dryRun);

            Given().Switch(out bool notGiven, "print, do not do").TryParse();
            Assert.IsFalse(notGiven);
        }

        [TestMethod]
        public void Typed_ArgumentsFillInDeclarationOrder()
        {
            Given("report.txt", "3")
                .Argument(out string file, "the file to read")
                .Argument(out int copies, "how many to write")
                .TryParse();

            Assert.AreEqual("report.txt", file);
            Assert.AreEqual(3, copies);
        }

        [TestMethod]
        public void Typed_AnOptionalArgumentLeftOutKeepsItsDefault()
        {
            var cmd = Given("report.txt")
                .Argument(out string file, "the file to read")
                .OptionalArgument(out int copies, "how many to write")
                .TryParse();

            Assert.IsFalse(cmd.ShouldExit);
            Assert.AreEqual("report.txt", file);
            Assert.AreEqual(0, copies);
        }

        [TestMethod]
        public void Typed_AValueThatWillNotConvertIsAReportedErrorAndNotAnException()
        {
            var cmd = Given("--queue-length:soon").Option(out int queueLength, "how many to queue").TryParse();

            Assert.AreEqual(0, queueLength);
            Assert.IsTrue(cmd.ShouldExit);
            Assert.AreEqual(1, cmd.ExitCode);
            StringAssert.Contains(this.Errors, "--queue-length expects a whole number, but got 'soon'");
        }

        [TestMethod]
        public void Typed_AnArgumentThatWillNotConvertNamesThePositional()
        {
            var cmd = Given("soon").Argument(out int queueLength, "how many to queue").TryParse();

            Assert.IsTrue(cmd.ShouldExit);
            StringAssert.Contains(this.Errors, "<queue-length> expects a whole number, but got 'soon'");
        }

        [TestMethod]
        public void Typed_AskingForHelpWinsOverAValueThatWouldNotConvert()
        {
            var cmd = Given("--queue-length:soon", "--help").Option(out int queueLength, "how many to queue").TryParse();

            Assert.IsTrue(cmd.HelpRequested);
            Assert.AreEqual(0, cmd.ExitCode);
            Assert.AreEqual(String.Empty, this.Errors);
        }

        [TestMethod]
        public void Typed_ConvertsTheEverydayTypes()
        {
            Given("--count:12", "--ratio:2.5", "--when:2026-09-04", "--wait:00:05:00",
                  "--id:8a3f9e1c-0000-0000-0000-000000000000", "--feed:https://nuget.org/v3/index.json",
                  "--verbose:yes", "--out:C:\\temp\\My-Folder")
                .Option(out int count, "how many to write")
                .Option(out double ratio, "the ratio to use")
                .Option(out DateTime when, "the date to report on")
                .Option(out TimeSpan wait, "how long to wait")
                .Option(out Guid id, "the id to report on")
                .Option(out Uri feed, "the feed to use")
                .Option(out bool verbose, "say more about it")
                .Option(out DirectoryInfo output, "where to write it", "out")
                .TryParse();

            Assert.AreEqual(12, count);
            Assert.AreEqual(2.5, ratio);
            Assert.AreEqual(new DateTime(2026, 9, 4), when);
            Assert.AreEqual(TimeSpan.FromMinutes(5), wait);
            Assert.AreEqual(new Guid("8a3f9e1c-0000-0000-0000-000000000000"), id);
            Assert.AreEqual("https://nuget.org/v3/index.json", feed.ToString());
            Assert.IsTrue(verbose);
            StringAssert.Contains(output.FullName, "My-Folder");
        }

        [TestMethod]
        public void Typed_ANumberIsReadTheSameWayWhateverTheMachineCallsADecimalPoint()
        {
            var was = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
                Given("--ratio:2.5").Option(out double ratio, "the ratio to use").TryParse();
                Assert.AreEqual(2.5, ratio);
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentCulture = was;
            }
        }

        [TestMethod]
        public void Typed_AnEnumIsMatchedWithoutRegardToCase()
        {
            foreach (var spelling in new[] { "--format:yaml", "--format:YAML", "--format:Yaml" })
            {
                Given(spelling).Option(out Format format, "the output format").TryParse();
                Assert.AreEqual(Format.Yaml, format, spelling);
            }
        }

        [TestMethod]
        public void Typed_AnEnumThatNamesNoMemberListsTheOnesItHas()
        {
            var cmd = Given("--format:toml").Option(out Format format, "the output format").TryParse();

            Assert.IsTrue(cmd.ShouldExit);
            Assert.AreEqual(Format.Json, format);
            StringAssert.Contains(this.Errors, "one of: Json, Xml, Yaml");
        }

        [TestMethod]
        public void Typed_ANumberThatNamesNoEnumMemberIsRefusedUnlessItIsFlags()
        {
            var cmd = Given("--format:7").Option(out Format format, "the output format").TryParse();
            Assert.IsTrue(cmd.ShouldExit);
            Assert.AreEqual(Format.Json, format);

            foreach (var spelling in new[] { "--trace:Reads,Writes", "--trace:reads,writes", "--trace:READS,WRITES" })
            {
                Given(spelling).Option(out Trace trace, "what to write to the log").TryParse();
                Assert.AreEqual(Trace.Reads | Trace.Writes, trace, spelling);
            }
        }

        [TestMethod]
        public void Typed_AnEnumOptionReadsBackTheMemberThatWasNamed()
        {
            foreach (var pair in new Dictionary<string, Color>
                     {
                         { "--color:Red", Color.Red },
                         { "--color:Green", Color.Green },
                         { "--color:Blue", Color.Blue },
                     })
            {
                Given(pair.Key).Option(out Color color, "the colour to draw in").TryParse();
                Assert.AreEqual(pair.Value, color, pair.Key);
            }
        }

        [TestMethod]
        public void Typed_AnEnumArgumentReadsBackTheMemberThatWasNamed()
        {
            foreach (var pair in new Dictionary<string, Color>
                     {
                         { "Red", Color.Red },
                         { "Green", Color.Green },
                         { "Blue", Color.Blue },
                     })
            {
                Given(pair.Key).Argument(out Color color, "the colour to draw in").TryParse();
                Assert.AreEqual(pair.Value, color, pair.Key);
            }
        }

        [TestMethod]
        public void Typed_AnEnumArgumentIgnoresCaseTheWayAnOptionDoes()
        {
            foreach (var spelling in new[] { "green", "GREEN", "Green", "gReEn" })
            {
                Given(spelling).Argument(out Color color, "the colour to draw in").TryParse();
                Assert.AreEqual(Color.Green, color, spelling);
            }
        }

        [TestMethod]
        public void Typed_AnEnumArgumentThatNamesNoMemberListsTheOnesItHas()
        {
            var cmd = Given("mauve").Argument(out Color color, "the colour to draw in").TryParse();

            Assert.IsTrue(cmd.ShouldExit);
            Assert.AreEqual(1, cmd.ExitCode);
            Assert.AreEqual(Color.Red, color);
            StringAssert.Contains(this.Errors, "<color> expects one of: Red, Green, Blue, but got 'mauve'");
        }

        [TestMethod]
        public void Typed_AnEnumOptionThatNamesNoMemberNamesTheSwitchInTheError()
        {
            var cmd = Given("--color:mauve").Option(out Color color, "the colour to draw in").TryParse();

            Assert.IsTrue(cmd.ShouldExit);
            StringAssert.Contains(this.Errors, "--color expects one of: Red, Green, Blue, but got 'mauve'");
        }

        [TestMethod]
        public void Typed_AnEnumTakesTheNumberOfAMemberButNotJustAnyNumber()
        {
            Given("2").Argument(out Color color, "the colour to draw in").TryParse();
            Assert.AreEqual(Color.Blue, color);

            var cmd = Given("9").Argument(out Color tooHigh, "the colour to draw in").TryParse();
            Assert.IsTrue(cmd.ShouldExit);
            Assert.AreEqual(Color.Red, tooHigh);
        }

        [TestMethod]
        public void Typed_AnEnumLeftOutIsItsZeroMemberUnlessItIsNullable()
        {
            // The trap worth knowing about: default(Color) is Red, so "not given" and "-color:Red"
            // are the same value. Color? is how a script tells them apart.
            var cmd = Given().OptionalArgument(out Color color, "the colour to draw in").TryParse();
            Assert.IsFalse(cmd.ShouldExit);
            Assert.AreEqual(Color.Red, color);

            Given().Option(out Color? chosen, "the colour to draw in").TryParse();
            Assert.IsNull(chosen);

            Given("--chosen:Blue").Option(out Color? given, "the colour to draw in", "chosen").TryParse();
            Assert.AreEqual(Color.Blue, given.Value);
        }

        [TestMethod]
        public void Typed_AnEnumIsDeclaredInTheHelpLikeAnyOtherValue()
        {
            var usage = Given()
                .Argument(out Color color, "the colour to draw in")
                .Option(out Color background, "the colour behind it")
                .TryParse().UsageText;

            StringAssert.Contains(usage, "<color>");
            StringAssert.Contains(usage, "--background:<value>");
            StringAssert.Contains(usage, "the colour to draw in");
        }

        [TestMethod]
        public void Typed_AliasesAreTheThirdArgument()
        {
            foreach (var spelling in new[] { "--queue-length:8", "-q:8", "--depth:8" })
            {
                Given(spelling).Option(out int queueLength, "how many to queue", "q|depth").TryParse();
                Assert.AreEqual(8, queueLength, spelling);
            }
        }

        [TestMethod]
        public void Typed_WhatIfReadsIntoABoolAndKeepsItsConventionalSpelling()
        {
            foreach (var spelling in new[] { "-whatif", "--dry-run", "-n" })
            {
                var cmd = Given(spelling).WhatIf(out bool rehearsing).TryParse();
                Assert.IsTrue(rehearsing, spelling);
                Assert.IsTrue(cmd.WhatIf, spelling);
            }
        }

        [TestMethod]
        public void Typed_UndeclaredSwitchesAreStillAnError()
        {
            var cmd = Given("--queue-length:8", "--nope").Option(out int queueLength, "how many to queue").TryParse();

            Assert.IsTrue(cmd.ShouldExit);
            StringAssert.Contains(this.Errors, "unknown switch");
        }

        [TestMethod]
        public void Typed_ASeparatedValueIsStillRefused()
        {
            var cmd = Given("--queue-length", "8").Option(out int queueLength, "how many to queue").TryParse();

            Assert.IsTrue(cmd.ShouldExit);
            Assert.AreEqual(0, queueLength);
            StringAssert.Contains(this.Errors, "needs a value, attached");
        }

        [TestMethod]
        public void Typed_TheValuesAreAlsoReadableFromTheResult()
        {
            var cmd = Given("--queue-length:8").Option(out int queueLength, "how many to queue").TryParse();

            Assert.AreEqual(8, queueLength);
            Assert.AreEqual("8", cmd.Option("queue-length"));
        }

        [TestMethod]
        public void Typed_CollidingWithSomethingAlreadyDeclaredStillThrows()
        {
            Assert.Throws<InvalidOperationException>(
                () => Given().Option("queuelength", "how many to queue").Option(out int queueLength, "how many to queue"));
        }

        [TestMethod]
        public void Typed_ATypeThatCannotBeMadeFromAStringThrowsAtTheDeclaration()
        {
            // Even with nothing on the command line, so it is found on the first run rather than
            // the first time somebody passes that switch.
            var thrown = Assert.Throws<NotSupportedException>(
                () => Given().Option(out int[] counts, "how many of each to write"));

            StringAssert.Contains(thrown.Message, "cannot be made from a string");
        }

        [TestMethod]
        public void Typed_ARestAfterATypedDeclarationThrowsRatherThanReadingItTwoWays()
        {
            var thrown = Assert.Throws<InvalidOperationException>(
                () => Given().Option(out int queueLength, "how many to queue").Rest("args", "what to pass on"));

            StringAssert.Contains(thrown.Message, "Rest()");
        }

        [TestMethod]
        public void Typed_ATypedOptionAfterARestReadsThePassThroughRulesCorrectly()
        {
            // Everything from the first positional on belongs to the wrapped program, --nope
            // included, so the typed option only sees what came before it.
            var cmd = Given("--timeout:30", "cmd.exe", "/k", "--nope")
                .Rest("args", "what to pass on")
                .Option(out int timeout, "seconds to wait for it")
                .TryParse();

            Assert.IsFalse(cmd.ShouldExit);
            Assert.AreEqual(30, timeout);
            CollectionAssert.AreEqual(new[] { "cmd.exe", "/k", "--nope" }, cmd.Rest.ToArray());
        }

        [TestMethod]
        public void Typed_NamesAreTakenOffTheVariableEvenWhenItWasDeclaredEarlier()
        {
            int queueLength;
            Given("--queue-length:8").Option(out queueLength, "how many to queue").TryParse();

            Assert.AreEqual(8, queueLength);
        }

        // ------------------------------------------------------------------ declaration mistakes

        [TestMethod]
        public void Declaring_AHelpTextThatLooksLikeAnAliasThrows()
        {
            // Switch("whatif", "n") -- the classic. Silently making "n" the help text is exactly
            // the quiet mistake this type exists to prevent, so it is refused.
            var thrown = Assert.Throws<ArgumentException>(() => Given().Switch("whatif", "n"));

            StringAssert.Contains(thrown.Message, "help text");
            StringAssert.Contains(thrown.Message, "whatif|n");
        }

        [TestMethod]
        public void Declaring_ANameWithItsPrefixThrows()
        {
            var thrown = Assert.Throws<ArgumentException>(() => Given().Switch("--whatif", "touch nothing"));
            StringAssert.Contains(thrown.Message, "without a prefix");
        }

        [TestMethod]
        public void Declaring_BlankHelpThrows()
        {
            Assert.Throws<ArgumentException>(() => Given().Switch("whatif", "   "));
        }

        [TestMethod]
        public void Declaring_TwoNamesThatNormaliseTheSameThrows()
        {
            // "nopush" and "no-push" are one switch once hyphens go. Better to refuse at
            // declaration than to silently have them share a value.
            var thrown = Assert.Throws<InvalidOperationException>(
                () => Given().Switch("nopush", "leave the push").Switch("no-push", "something else"));

            StringAssert.Contains(thrown.Message, "collides");
        }

        [TestMethod]
        public void Declaring_ASwitchAndAnArgumentWithOneNameThrows()
        {
            Assert.Throws<InvalidOperationException>(
                () => Given().Argument("out", "where to write").Switch("out", "something else"));
        }

        [TestMethod]
        public void Declaring_ARequiredArgumentAfterAnOptionalOneThrows()
        {
            var thrown = Assert.Throws<InvalidOperationException>(
                () => Given().OptionalArgument("repo", "the repo").Argument("branch", "the branch"));

            StringAssert.Contains(thrown.Message, "must be last");
        }

        [TestMethod]
        public void Declaring_AnythingAfterARestThrows()
        {
            Assert.Throws<InvalidOperationException>(
                () => Given().Rest("args", "passed through").Argument("file", "a file"));
        }

        [TestMethod]
        public void Declaring_AnAliasOnAnArgumentThrows()
        {
            var thrown = Assert.Throws<ArgumentException>(() => Given().Argument("file|f", "a file"));
            StringAssert.Contains(thrown.Message, "matched by position");
        }

        // ------------------------------------------------------------------ options

        [TestMethod]
        public void Option_TakesItsValueAfterAColonOrAnEquals()
        {
            Assert.AreEqual("test", Given("-folder:test").Option("folder", "the folder").TryParse().Option("folder"));
            Assert.AreEqual("test", Given("-folder=test").Option("folder", "the folder").TryParse().Option("folder"));
            Assert.AreEqual("test", Given("--folder:test").Option("folder", "the folder").TryParse().Option("folder"));

        }

        [TestMethod]
        public void Option_IsNullWhenNotGiven()
        {
            Assert.IsNull(Given().Option("folder", "the folder").TryParse().Option("folder"));
        }

        [TestMethod]
        public void Option_NameIsNormalisedButTheValueIsNot()
        {
            // The whole point of splitting before normalising: --API-KEY finds the option, and
            // the key it carries is untouched.
            var cmd = Given("--API-KEY:sk-ant-AbC123").Option("api-key", "the key").TryParse();

            Assert.AreEqual("sk-ant-AbC123", cmd.Option("api-key"));
        }

        [TestMethod]
        public void Option_ValueKeepsItsCaseAndHyphens()
        {
            var cmd = Given(@"-out:C:\temp\My-Folder").Option("out", "where to write").TryParse();

            Assert.AreEqual(@"C:\temp\My-Folder", cmd.Option("out"));
        }

        [TestMethod]
        public void Option_SplitsOnTheFirstSeparatorOnlySoAValueMayContainMore()
        {
            Assert.AreEqual("https://api.nuget.org/v3/index.json",
                Given("-source:https://api.nuget.org/v3/index.json").Option("source", "the feed").TryParse().Option("source"));

            Assert.AreEqual("a=b=c", Given("-q:a=b=c").Option("q", "a query").TryParse().Option("q"));
            Assert.AreEqual(@"C:\temp", Given(@"-out=C:\temp").Option("out", "where to write").TryParse().Option("out"));
        }

        [TestMethod]
        public void Option_ApiKeyAndApikeyAreTheSameOption()
        {
            foreach (var spelling in new[] { "--api-key:x", "--apikey:x", "-API_KEY:x" })
            {
                Assert.AreEqual("x", Given(spelling).Option("api-key", "the key").TryParse().Option("api-key"), spelling);
            }
        }

        [TestMethod]
        public void Option_GivenBareIsAnErrorNamingTheAttachedForm()
        {
            // This is what catches someone typing the separated "--folder test" habit, instead of
            // letting "test" slide through as a positional.
            var cmd = Given("-folder").Option("folder", "the folder").TryParse();

            Assert.IsTrue(cmd.ShouldExit);
            Assert.AreEqual(1, cmd.ExitCode);
            StringAssert.Contains(this.Errors, "needs a value");
            StringAssert.Contains(this.Errors, "--folder:value");
        }

        [TestMethod]
        public void Option_GivenAnEmptyValueIsAnError()
        {
            Assert.IsTrue(Given("-folder:").Option("folder", "the folder").TryParse().ShouldExit);
            StringAssert.Contains(this.Errors, "needs a value");
        }

        [TestMethod]
        public void Option_GivenTwiceIsAnError()
        {
            var cmd = Given("-source:a", "-source:b").Option("source", "the feed").TryParse();

            Assert.IsTrue(cmd.ShouldExit);
            StringAssert.Contains(this.Errors, "more than once");
        }

        [TestMethod]
        public void Option_ErrorMessagesNeverEchoTheValue()
        {
            // A secret must not reach stderr because the user typed it twice, or typed the
            // separated form and left it dangling.
            Given("--api-key:sk-ant-SECRET", "--api-key:sk-ant-OTHER").Option("api-key", "the key").TryParse();
            Assert.IsFalse(this.Errors.Contains("SECRET"), "an option's value must never be echoed");
            Assert.IsFalse(this.Errors.Contains("OTHER"), "an option's value must never be echoed");

            Capture();
            Given("--api-key", "sk-ant-SECRET").Option("api-key", "the key").TryParse();
            Assert.IsFalse(this.Errors.Contains("SECRET"),
                "the token after a bare option must not be echoed as an unexpected argument either");
        }

        // ------------------------------------------------------------------ unknown switches

        [TestMethod]
        public void Unknown_SwitchIsAnErrorNamingTheRawToken()
        {
            var cmd = Given("--dryrun").Switch("nopush", "leave the push").TryParse();

            Assert.IsTrue(cmd.ShouldExit);
            Assert.AreEqual(1, cmd.ExitCode);
            StringAssert.Contains(this.Errors, "unknown switch '--dryrun'");
        }

        [TestMethod]
        public void Unknown_SwitchGoesToStandardErrorNotStandardOut()
        {
            Given("--nope").Switch("whatif", "touch nothing").TryParse();

            StringAssert.Contains(this.Errors, "unknown switch");
            Assert.AreEqual(String.Empty, this.Screen, "an error is not output");
        }

        [TestMethod]
        public void Unknown_SwitchPointsAtHelpRatherThanPrintingIt()
        {
            Given("--nope").Switch("whatif", "touch nothing").TryParse();

            StringAssert.Contains(this.Errors, "Try 'demo --help'");
            Assert.IsFalse(this.Errors.Contains("Switches:"), "the full usage is noise here");
        }

        [TestMethod]
        public void Unknown_SwitchesAreAllReportedAtOnce()
        {
            Given("--nope", "--alsonope").Switch("whatif", "touch nothing").TryParse();

            StringAssert.Contains(this.Errors, "'--nope'");
            StringAssert.Contains(this.Errors, "'--alsonope'");
        }

        [TestMethod]
        public void Unknown_ATypoThatNormalisesToADeclaredSwitchIsNotUnknown()
        {
            // "--dryrun" for "--dry-run" is the bug this closes: today it becomes a path.
            Assert.IsTrue(Given("--dryrun").Switch("dry-run", "print only").TryParse().Switch("dry-run"));
        }

        [TestMethod]
        public void Unknown_SwitchErrorsSuppressPositionalErrors()
        {
            var cmd = Given("--nope", "extra1", "extra2").Switch("whatif", "touch nothing").TryParse();

            Assert.IsTrue(cmd.ShouldExit);
            StringAssert.Contains(this.Errors, "unknown switch");
            Assert.IsFalse(this.Errors.Contains("unexpected"),
                "once the switches were misread the positional list means nothing");
        }

        // ------------------------------------------------------------------ positionals

        [TestMethod]
        public void Argument_FillsInDeclarationOrder()
        {
            var cmd = Given("in.txt", "out").Argument("file", "the file").Argument("output", "the folder").TryParse();

            Assert.AreEqual("in.txt", cmd.Argument("file"));
            Assert.AreEqual("out", cmd.Argument("output"));
        }

        [TestMethod]
        public void Argument_MayBeInterspersedWithSwitches()
        {
            var cmd = Given("repo", "-whatif").OptionalArgument("repo", "the repo").Switch("whatif", "touch nothing").TryParse();

            Assert.AreEqual("repo", cmd.Argument("repo"));
            Assert.IsTrue(cmd.Switch("whatif"));
        }

        [TestMethod]
        public void Argument_MissingRequiredIsAnErrorNamingIt()
        {
            var cmd = Given().Argument("file", "the file").TryParse();

            Assert.IsTrue(cmd.ShouldExit);
            Assert.AreEqual(1, cmd.ExitCode);
            StringAssert.Contains(this.Errors, "missing <file>");
        }

        [TestMethod]
        public void Argument_OptionalMayBeOmittedAndReadsNull()
        {
            Assert.IsNull(Given().OptionalArgument("path", "the path").TryParse().Argument("path"));
            Assert.AreEqual("x", Given("x").OptionalArgument("path", "the path").TryParse().Argument("path"));
        }

        [TestMethod]
        public void Argument_TooManyIsAnErrorNamingTheUnexpectedOnes()
        {
            var one = Given("a", "b").OptionalArgument("path", "the path").TryParse();
            Assert.IsTrue(one.ShouldExit);
            StringAssert.Contains(this.Errors, "unexpected argument 'b'");

            Capture();
            Given("a", "b", "c").OptionalArgument("path", "the path").TryParse();
            StringAssert.Contains(this.Errors, "unexpected arguments: 'b' 'c'");
        }

        [TestMethod]
        public void Argument_PathsAreNotMistakenForSwitches()
        {
            // The reason '/' is recognised rather than demanded: an absolute path on Linux starts
            // with one.
            Assert.AreEqual("/home/tom/file",
                Given("/home/tom/file").OptionalArgument("path", "the path").TryParse().Argument("path"));

            Capture();
            Assert.AreEqual(@"C:\temp",
                Given(@"C:\temp").OptionalArgument("path", "the path").TryParse().Argument("path"));

            Capture();
            Assert.AreEqual("/tmp/x:y",
                Given("/tmp/x:y").OptionalArgument("path", "the path").TryParse().Argument("path"));

            Capture();
            Assert.AreEqual("/usr/local/bin",
                Given("/usr/local/bin").OptionalArgument("path", "the path").TryParse().Argument("path"));
        }

        [TestMethod]
        public void Argument_ASlashTokenIsAlwaysAPositional()
        {
            // '/' is not a switch prefix. Dashes are the standard, and treating '/' as a prefix
            // would make every absolute path on Linux something the parser had to recognise.
            Assert.AreEqual("/nope", Given("/nope").OptionalArgument("path", "the path").TryParse().Argument("path"));

            Capture();
            var cmd = Given("/whatif").OptionalArgument("path", "the path").Switch("whatif", "touch nothing").TryParse();
            Assert.AreEqual("/whatif", cmd.Argument("path"), "a slash token is a value, not the switch it resembles");
            Assert.IsFalse(cmd.Switch("whatif"));
        }

        [TestMethod]
        public void Argument_ADashTokenIsAnUnknownSwitchNotAPositional()
        {
            Assert.IsTrue(Given("-nope").OptionalArgument("path", "the path").TryParse().ShouldExit);
            StringAssert.Contains(this.Errors, "unknown switch");
        }

        [TestMethod]
        public void Argument_NegativeNumbersAndABareDashArePositionals()
        {
            Assert.AreEqual("-9", Given("-9").OptionalArgument("n", "a number").TryParse().Argument("n"));

            Capture();
            Assert.AreEqual("-", Given("-").OptionalArgument("n", "stdin").TryParse().Argument("n"));
        }

        [TestMethod]
        public void Argument_AfterTheTerminatorMayLookLikeASwitch()
        {
            var cmd = Given("--", "-weird-name").OptionalArgument("path", "the path").TryParse();

            Assert.AreEqual("-weird-name", cmd.Argument("path"));
        }

        [TestMethod]
        public void Argument_TheTerminatorIsNotItselfAPositional()
        {
            var cmd = Given("a", "--", "b").Argument("one", "first").OptionalArgument("two", "second").TryParse();

            Assert.AreEqual("a", cmd.Argument("one"));
            Assert.AreEqual("b", cmd.Argument("two"));
        }

        [TestMethod]
        public void Argument_ReadingAnUndeclaredNameThrows()
        {
            var cmd = Given("x").OptionalArgument("path", "the path").TryParse();

            Assert.Throws<ArgumentException>(() => cmd.Argument("nope"));
        }

        [TestMethod]
        public void Argument_ArgumentsListsEveryPositionalInOrder()
        {
            var cmd = Given("a", "b").Argument("one", "first").Argument("two", "second").TryParse();

            CollectionAssert.AreEqual(new[] { "a", "b" }, cmd.Arguments.ToArray());
        }

        // ------------------------------------------------------------------ rest

        [TestMethod]
        public void Rest_CollectsWhatIsLeftVerbatim()
        {
            var cmd = Given("cmd.exe", "/k", "dir").Argument("program", "what to run").Rest("args", "passed through").TryParse();

            Assert.AreEqual("cmd.exe", cmd.Argument("program"));
            CollectionAssert.AreEqual(new[] { "/k", "dir" }, cmd.Rest.ToArray());
        }

        [TestMethod]
        public void Rest_StopsSwitchParsingAtTheFirstPositional()
        {
            // -whatif is ours because it comes first; --help belongs to the child.
            var cmd = Given("-whatif", "cmd.exe", "--help")
                .Switch("whatif", "touch nothing")
                .Argument("program", "what to run")
                .Rest("args", "passed through")
                .TryParse();

            Assert.IsFalse(cmd.ShouldExit, "--help after the program name is the child's, not ours");
            Assert.IsTrue(cmd.Switch("whatif"));
            CollectionAssert.AreEqual(new[] { "--help" }, cmd.Rest.ToArray());
        }

        [TestMethod]
        public void Rest_StillRejectsAMistypedSwitchBeforeTheFirstPositional()
        {
            // The reason the boundary is the first positional rather than the first unrecognised
            // switch: otherwise a typo is silently handed to the child.
            var cmd = Given("--whatf", "cmd.exe")
                .Switch("whatif", "touch nothing")
                .Argument("program", "what to run")
                .Rest("args", "passed through")
                .TryParse();

            Assert.IsTrue(cmd.ShouldExit);
            StringAssert.Contains(this.Errors, "unknown switch '--whatf'");
        }

        [TestMethod]
        public void Rest_IsEmptyWhenNothingIsLeft()
        {
            var cmd = Given("cmd.exe").Argument("program", "what to run").Rest("args", "passed through").TryParse();

            Assert.AreEqual(0, cmd.Rest.Count);
        }

        [TestMethod]
        public void Rest_ReadingItUndeclaredThrows()
        {
            var cmd = Given().Switch("whatif", "touch nothing").TryParse();

            Assert.Throws<InvalidOperationException>(() => { var ignored = cmd.Rest; });
        }

        // ------------------------------------------------------------------ help

        [TestMethod]
        public void Help_IsUnderstoodWithoutBeingDeclared()
        {
            foreach (var spelling in new[] { "--help", "-h", "-?" })
            {
                Capture();
                var cmd = Given(spelling).Switch("whatif", "touch nothing").TryParse();

                Assert.IsTrue(cmd.ShouldExit, spelling);
                Assert.IsTrue(cmd.HelpRequested, spelling);
                Assert.AreEqual(0, cmd.ExitCode, spelling);
                StringAssert.Contains(this.Screen, "Usage:", spelling);
            }
        }

        [TestMethod]
        public void Help_GoesToStandardOutNotStandardError()
        {
            Given("--help").Switch("whatif", "touch nothing").TryParse();

            StringAssert.Contains(this.Screen, "Usage:");
            Assert.AreEqual(String.Empty, this.Errors);
        }

        [TestMethod]
        public void Help_WinsOverAnUnknownSwitchAndAMissingArgument()
        {
            var cmd = Given("--nope", "--help").Argument("file", "the file").TryParse();

            Assert.IsTrue(cmd.HelpRequested);
            Assert.AreEqual(0, cmd.ExitCode);
        }

        [TestMethod]
        public void Help_ListsEveryDeclaredArgumentSwitchAndOption()
        {
            // The anti-drift guarantee: the help cannot fall out of step with what is accepted,
            // because it is rendered from the same declarations.
            Given("--help")
                .Description("Does a thing.")
                .Argument("file", "File to operate on")
                .OptionalArgument("output", "output folder")
                .Switch("whatif", "What if without execute")
                .Option("source", "the feed to use")
                .TryParse();

            StringAssert.Contains(this.Screen, "Does a thing.");
            StringAssert.Contains(this.Screen, "file");
            StringAssert.Contains(this.Screen, "File to operate on");
            StringAssert.Contains(this.Screen, "output folder");
            StringAssert.Contains(this.Screen, "--whatif");
            StringAssert.Contains(this.Screen, "What if without execute");
            StringAssert.Contains(this.Screen, "--source:<value>");
            StringAssert.Contains(this.Screen, "--help");
        }

        [TestMethod]
        public void Help_ShowsRequiredAndOptionalArgumentsDifferently()
        {
            Given("--help").Argument("file", "the file").OptionalArgument("output", "the folder").TryParse();

            StringAssert.Contains(this.Screen, "<file>");
            StringAssert.Contains(this.Screen, "[output]");
        }

        [TestMethod]
        public void Help_ShowsARestWithAnEllipsis()
        {
            Given("--help").Argument("program", "what to run").Rest("args", "passed through").TryParse();

            StringAssert.Contains(this.Screen, "[args...]");
        }

        [TestMethod]
        public void Help_ListsAliasesBesideTheirSwitch()
        {
            Given("--help").Switch("whatif|dry-run|n", "touch nothing").TryParse();

            StringAssert.Contains(this.Screen, "--whatif, --dry-run, -n");
        }

        [TestMethod]
        public void Help_NamesTheProgram()
        {
            Given("--help").Switch("whatif", "touch nothing").TryParse();

            StringAssert.Contains(this.Screen, "demo");
        }

        [TestMethod]
        public void Help_NamesTheProgramWithoutBeingToldWhoItIs()
        {
            // A .csx or .csrun is named after its own file -- verified by hand under dotnet-script,
            // and untestable from here because this caller is a compiled .cs. What IS testable is
            // that the fallback never leaves the usage line blank, and that Program() wins.
            var inferred = Cli.For(new string[0]).Switch("whatif", "touch nothing").TryParse();
            Assert.IsFalse(String.IsNullOrWhiteSpace(inferred.ProgramName));

            var told = Cli.For(new string[0]).Program("gho").Switch("whatif", "touch nothing").TryParse();
            Assert.AreEqual("gho", told.ProgramName);
        }

        [TestMethod]
        public void Help_DedentsTheDescription()
        {
            Given("--help").Description(@"
                First line.
                  Indented under it.").TryParse();

            StringAssert.Contains(this.Screen, "First line.");
            StringAssert.Contains(this.Screen, "  Indented under it.");
            Assert.IsFalse(this.Screen.Contains("                First line."));
        }

        [TestMethod]
        public void Help_IncludesExamples()
        {
            Given("--help").Switch("whatif", "touch nothing")
                .Example("demo -whatif", "show what would happen")
                .TryParse();

            StringAssert.Contains(this.Screen, "Examples:");
            StringAssert.Contains(this.Screen, "demo -whatif");
            StringAssert.Contains(this.Screen, "show what would happen");
        }

        [TestMethod]
        public void Help_IsReadableAsAStringWithoutTouchingTheConsole()
        {
            var cmd = Given().Switch("whatif", "touch nothing").TryParse();

            StringAssert.Contains(cmd.UsageText, "Usage:");
            Assert.AreEqual(String.Empty, this.Screen);
        }

        [TestMethod]
        public void Help_CanBeReplacedByTheScriptsOwn()
        {
            Given("--help").Switch("help|h", "show the help my way").TryParse();

            StringAssert.Contains(this.Screen, "show the help my way");
            Assert.IsFalse(this.Screen.Contains("show this help"));
        }

        // ------------------------------------------------------------------ usage when empty

        [TestMethod]
        public void UsageWhenEmpty_PrintsUsageAndExitsZeroForNoArguments()
        {
            var cmd = Given().UsageWhenEmpty().Argument("file", "the file").TryParse();

            Assert.IsTrue(cmd.ShouldExit);
            Assert.AreEqual(0, cmd.ExitCode, "being shown the usage is not a failure");
            StringAssert.Contains(this.Screen, "Usage:");
        }

        [TestMethod]
        public void UsageWhenEmpty_IsOffUnlessAskedFor()
        {
            var cmd = Given().Argument("file", "the file").TryParse();

            Assert.AreEqual(1, cmd.ExitCode, "without it, a missing required argument is still an error");
            StringAssert.Contains(this.Errors, "missing <file>");
        }

        [TestMethod]
        public void UsageWhenEmpty_IsNotTriggeredWhenAnythingIsGiven()
        {
            var cmd = Given("x").UsageWhenEmpty().Argument("file", "the file").TryParse();

            Assert.IsFalse(cmd.ShouldExit);
            Assert.AreEqual("x", cmd.Argument("file"));
        }

        // ------------------------------------------------------------------ whatif

        [TestMethod]
        public void WhatIf_AcceptsAllThreeSpellings()
        {
            foreach (var spelling in new[] { "-whatif", "--dry-run", "--dryrun", "-n" })
            {
                Assert.IsTrue(Given(spelling).WhatIf().TryParse().WhatIf, spelling);
            }
        }

        [TestMethod]
        public void WhatIf_IsFalseWhenNotGiven()
        {
            Assert.IsFalse(Given().WhatIf().TryParse().WhatIf);
        }

        [TestMethod]
        public void WhatIf_ReadingItUndeclaredThrowsRatherThanAnsweringFalse()
        {
            // Answering false would mean a script that forgot .WhatIf() silently never rehearses.
            var cmd = Given().Switch("nopush", "leave the push").TryParse();

            var thrown = Assert.Throws<InvalidOperationException>(() => { var ignored = cmd.WhatIf; });
            StringAssert.Contains(thrown.Message, "never declared");
        }

        [TestMethod]
        public void WhatIf_ShowsWhatIfAsItsPrimarySpelling()
        {
            Given("--help").WhatIf().TryParse();

            StringAssert.Contains(this.Screen, "--whatif");
        }

        // ------------------------------------------------------------------ the result contract

        [TestMethod]
        public void Parse_IsQuietAndReadableForACleanCommandLine()
        {
            var cmd = Given("-whatif").Switch("whatif", "touch nothing").TryParse();

            Assert.IsFalse(cmd.ShouldExit);
            Assert.AreEqual(0, cmd.ExitCode);
            Assert.IsNull(cmd.Error);
            Assert.IsFalse(cmd.HelpRequested);
            Assert.AreEqual(String.Empty, this.Screen);
            Assert.AreEqual(String.Empty, this.Errors);
        }

        [TestMethod]
        public void Parse_ReadingAnythingAfterAnErrorThrows()
        {
            // The guard under the ShouldExit contract: a script that forgets the check fails
            // loudly instead of running on with defaults it never earned.
            var cmd = Given("--nope").Switch("whatif", "touch nothing").Argument("file", "the file").TryParse();

            Assert.IsTrue(cmd.ShouldExit);
            Assert.Throws<InvalidOperationException>(() => cmd.Switch("whatif"));
            Assert.Throws<InvalidOperationException>(() => cmd.Argument("file"));
        }

        [TestMethod]
        public void Parse_TheDiagnosticsStayReadableAfterAnError()
        {
            var cmd = Given("--nope").Switch("whatif", "touch nothing").TryParse();

            Assert.IsTrue(cmd.ShouldExit);
            Assert.AreEqual(1, cmd.ExitCode);
            StringAssert.Contains(cmd.Error, "unknown switch");
            StringAssert.Contains(cmd.UsageText, "Usage:");
            Assert.AreEqual("demo", cmd.ProgramName);
        }

        [TestMethod]
        public void Parse_TakesAnArrayOrAList()
        {
            Assert.IsTrue(Cli.For(new List<string> { "-whatif" }).Program("demo")
                .Switch("whatif", "touch nothing").TryParse().Switch("whatif"));

            Assert.IsTrue(Cli.For(new[] { "-whatif" }).Program("demo")
                .Switch("whatif", "touch nothing").TryParse().Switch("whatif"));
        }

        [TestMethod]
        public void Parse_NullArgumentsThrows()
        {
            Assert.Throws<ArgumentNullException>(() => Cli.For(null));
        }

        [TestMethod]
        public void Parse_AnEmptyCommandLineIsFineWhenNothingIsRequired()
        {
            var cmd = Given().Switch("whatif", "touch nothing").TryParse();

            Assert.IsFalse(cmd.ShouldExit);
            Assert.IsFalse(cmd.Switch("whatif"));
        }

        // ------------------------------------------------------------------ commands

        [TestMethod]
        public void Command_DispatchesToTheCommandThatWasTyped()
        {
            var ran = "nothing";

            Given("start")
                .Command("start", "start the service", c => c.Run(() => { ran = "start"; }))
                .Command("stop", "stop the service", c => c.Run(() => { ran = "stop"; }))
                .TryParse();

            Assert.AreEqual("start", ran);
        }

        [TestMethod]
        public void Command_IsMatchedIgnoringCaseHyphensAndUnderscores()
        {
            foreach (var typed in new[] { "dry-run", "dryrun", "DRY_RUN", "Dry-Run" })
            {
                Capture();
                var ran = false;
                var cmd = Given(typed).Command("dry-run", "rehearse it", c => c.Run(() => { ran = true; })).TryParse();

                Assert.IsFalse(cmd.ShouldExit, typed);
                Assert.IsTrue(ran, typed);
            }
        }

        [TestMethod]
        public void Command_AliasesAfterThePipeSelectTheSameCommand()
        {
            foreach (var typed in new[] { "remove", "rm" })
            {
                Capture();
                var cmd = Given(typed).Command("remove|rm", "take one away", c => { }).TryParse();

                Assert.IsFalse(cmd.ShouldExit, typed);
                Assert.AreEqual("remove", cmd.Command, "the primary spelling, whichever alias was typed");
            }
        }

        [TestMethod]
        public void Command_ReadsBackOffTheResultForScriptsThatSwitchThemselves()
        {
            // The other half of the contract: a script that would rather not declare a handler
            // reads which command was chosen and takes its values off the same result.
            var cmd = Given("start", "foo.txt", "--force")
                .Command("start", "start it", c => c
                    .Argument("file", "the file")
                    .Switch("force", "start anyway"))
                .Command("stop", "stop it", c => c.Option("timeout", "seconds to wait"))
                .TryParse();

            Assert.AreEqual("start", cmd.Command);
            Assert.AreEqual("foo.txt", cmd.Argument("file"));
            Assert.IsTrue(cmd.Switch("force"));
        }

        [TestMethod]
        public void Command_OnlyTheMatchedCommandsDeclarationsAreEverRun()
        {
            // What makes typed variables safe inside a command: nothing binds for a command
            // nobody asked for.
            var built = new List<string>();

            Given("start")
                .Command("start", "start it", c => { built.Add("start"); })
                .Command("stop", "stop it", c => { built.Add("stop"); })
                .TryParse();

            CollectionAssert.AreEqual(new[] { "start" }, built.ToArray());
        }

        [TestMethod]
        public void Command_NestsToAnyDepth()
        {
            string pushed = null;

            var cmd = Given("nuget", "push", "x.nupkg")
                .Command("nuget", "work with the feed", c => c
                    .Command("push", "push a package", p =>
                    {
                        p.Argument(out string package, "the .nupkg to push");
                        p.Run(() => { pushed = package; });
                    }))
                .TryParse();

            Assert.IsFalse(cmd.ShouldExit);
            Assert.AreEqual("x.nupkg", pushed);
            Assert.AreEqual("demo nuget push", cmd.ProgramName);
        }

        [TestMethod]
        public void Command_PathNamesEveryLevelThatWasTyped()
        {
            var cmd = Given("nuget", "push", "x.nupkg")
                .Command("nuget", "work with the feed", c => c
                    .Command("push", "push a package", p => p.Argument("package", "the .nupkg")))
                .TryParse();

            CollectionAssert.AreEqual(new[] { "nuget", "push" }, cmd.CommandPath.ToArray());
            Assert.AreEqual("push", cmd.Command);
        }

        [TestMethod]
        public void Command_TheResultIsTheLeafAndKnowsItsParent()
        {
            var cmd = Given("start", "foo.txt")
                .Command("start", "start it", c => c.Argument("file", "the file"))
                .TryParse();

            Assert.AreEqual("demo start", cmd.ProgramName);
            Assert.IsNotNull(cmd.Parent);
            Assert.AreEqual("demo", cmd.Parent.ProgramName);
            Assert.IsNull(cmd.Parent.Command);
        }

        [TestMethod]
        public void Command_TypedVariablesAreBoundFromTheTokensAfterTheCommandWord()
        {
            string seen = null;
            var forced = false;

            Given("start", "foo.txt", "--force")
                .Command("start", "start it", c =>
                {
                    c.Argument(out string file, "the file to start");
                    c.Switch(out bool force, "start it anyway");
                    c.Run(() => { seen = file; forced = force; });
                })
                .TryParse();

            Assert.AreEqual("foo.txt", seen);
            Assert.IsTrue(forced);
        }

        [TestMethod]
        public void Command_AMissingArgumentIsReportedAgainstTheCommandNotTheProgram()
        {
            var cmd = Given("start").Command("start", "start it", c => c.Argument("file", "the file")).TryParse();

            Assert.IsTrue(cmd.ShouldExit);
            StringAssert.Contains(this.Errors, "demo start: missing <file>");
            StringAssert.Contains(this.Errors, "Try 'demo start --help'");
        }

        [TestMethod]
        public void Command_ARestInsideACommandCollectsWhatFollowsIt()
        {
            var cmd = Given("exec", "cmd.exe", "/k", "dir")
                .Command("exec", "run something", c => c
                    .Argument("program", "what to run")
                    .Rest("args", "passed through"))
                .TryParse();

            Assert.AreEqual("cmd.exe", cmd.Argument("program"));
            CollectionAssert.AreEqual(new[] { "/k", "dir" }, cmd.Rest.ToArray());
        }

        [TestMethod]
        public void Command_GlobalSwitchesBeforeTheCommandAreTheProgramsOwn()
        {
            var cmd = Given("--verbose", "start")
                .Command("start", "start it", c => { })
                .Switch("verbose", "say what is happening")
                .TryParse();

            Assert.IsFalse(cmd.ShouldExit);
            Assert.AreEqual("start", cmd.Command);
            Assert.IsTrue(cmd.Switch("verbose"), "a global is readable from the command's own result");
        }

        [TestMethod]
        public void Command_TheProgramsOptionsAndWhatIfAreReadableFromTheCommandsResult()
        {
            var cmd = Given("--source:https://nuget.org", "-whatif", "push")
                .Command("push", "push a package", c => { })
                .Option("source", "the feed to use")
                .WhatIf()
                .TryParse();

            Assert.AreEqual("https://nuget.org", cmd.Option("source"));
            Assert.IsTrue(cmd.WhatIf);
        }

        [TestMethod]
        public void Command_ATypedGlobalBindsOnlyFromBeforeTheCommandWord()
        {
            // Declared after the first Command(), so the boundary is known when it reads.
            var cli = Given("--verbose", "start", "foo.txt")
                .Command("start", "start it", c => c.Argument("file", "the file"))
                .Switch(out bool verbose, "say what is happening");

            Assert.IsTrue(verbose);
            Assert.IsFalse(cli.TryParse().ShouldExit);
        }

        [TestMethod]
        public void Command_AGlobalWrittenAfterTheCommandIsRejectedAndSaysWhereItGoes()
        {
            var cmd = Given("start", "--verbose")
                .Command("start", "start it", c => { })
                .Switch("verbose", "say what is happening")
                .TryParse();

            Assert.IsTrue(cmd.ShouldExit);
            StringAssert.Contains(this.Errors, "'--verbose' is a global switch");
            StringAssert.Contains(this.Errors, "'demo --verbose start'");
        }

        [TestMethod]
        public void Command_AnUnknownGlobalIsReportedBeforeTheCommandIsResolved()
        {
            var built = false;
            var cmd = Given("--nope", "start").Command("start", "start it", c => { built = true; }).TryParse();

            Assert.IsTrue(cmd.ShouldExit);
            StringAssert.Contains(this.Errors, "demo: unknown switch '--nope'");
            Assert.IsFalse(built, "once the globals were misread there is nothing to say about the rest");
        }

        [TestMethod]
        public void Command_AnUnknownSwitchInsideACommandNamesTheCommandInTheError()
        {
            var cmd = Given("start", "--nope").Command("start", "start it", c => { }).TryParse();

            Assert.IsTrue(cmd.ShouldExit);
            StringAssert.Contains(this.Errors, "demo start: unknown switch '--nope'");
        }

        [TestMethod]
        public void Command_ADoubleDashEndsTheGlobalsAndTheNextWordIsTheCommand()
        {
            var cmd = Given("--", "start").Command("start", "start it", c => { }).TryParse();

            Assert.IsFalse(cmd.ShouldExit);
            Assert.AreEqual("start", cmd.Command);
        }

        [TestMethod]
        public void Command_NoCommandGivenIsAnErrorThatListsTheCommands()
        {
            var cmd = Given()
                .Command("start", "start it", c => { })
                .Command("stop", "stop it", c => { })
                .TryParse();

            Assert.IsTrue(cmd.ShouldExit);
            Assert.AreEqual(1, cmd.ExitCode);
            StringAssert.Contains(this.Errors, "demo: no command given -- one of: start, stop.");
            StringAssert.Contains(this.Errors, "Try 'demo --help' for the commands it takes.");
        }

        [TestMethod]
        public void Command_AnUnknownCommandIsAnErrorThatListsTheCommands()
        {
            var cmd = Given("strat").Command("start", "start it", c => { }).TryParse();

            Assert.IsTrue(cmd.ShouldExit);
            StringAssert.Contains(this.Errors, "demo: unknown command 'strat' -- expected one of: start.");
        }

        [TestMethod]
        public void Command_UsageWhenEmptyPrintsTheCommandListAndExitsZero()
        {
            var cmd = Given().UsageWhenEmpty().Command("start", "start it", c => { }).TryParse();

            Assert.IsTrue(cmd.ShouldExit);
            Assert.AreEqual(0, cmd.ExitCode);
            StringAssert.Contains(this.Screen, "Commands:");
            Assert.AreEqual(String.Empty, this.Errors);
        }

        [TestMethod]
        public void Help_ListsCommandsWithTheirAliasesAndTeachesTheGrammar()
        {
            Given("--help")
                .Description("Manages the service.")
                .Command("start", "start the service", c => { })
                .Command("remove|rm", "take one away", c => { })
                .Switch("verbose", "say what is happening")
                .TryParse();

            StringAssert.Contains(this.Screen, "Manages the service.");
            StringAssert.Contains(this.Screen, "Commands:");
            StringAssert.Contains(this.Screen, "start the service");
            StringAssert.Contains(this.Screen, "remove, rm");
            StringAssert.Contains(this.Screen, "<command> ...");
            StringAssert.Contains(this.Screen, "See 'demo <command> --help' for what a command takes.");
        }

        [TestMethod]
        public void Help_AfterTheCommandIsTheCommandsOwnHelp()
        {
            var cmd = Given("start", "--help")
                .Command("start", "start it", c => c.Argument("file", "the file to start"))
                .TryParse();

            Assert.IsTrue(cmd.HelpRequested);
            Assert.AreEqual(0, cmd.ExitCode);
            Assert.AreEqual("demo start", cmd.ProgramName);
            StringAssert.Contains(this.Screen, "demo start <file>");
            StringAssert.Contains(this.Screen, "the file to start");
        }

        [TestMethod]
        public void Help_BeforeTheCommandIsTheProgramsAndDoesNotBuildIt()
        {
            var built = false;
            Given("--help", "start").Command("start", "start it", c => { built = true; }).TryParse();

            StringAssert.Contains(this.Screen, "Commands:");
            Assert.IsFalse(built, "listing the commands does not build any of them");
        }

        [TestMethod]
        public void Help_WinsOverAnUnknownCommand()
        {
            var cmd = Given("--help", "strat").Command("start", "start it", c => { }).TryParse();

            Assert.IsTrue(cmd.HelpRequested);
            Assert.AreEqual(0, cmd.ExitCode);
        }

        [TestMethod]
        public void Run_TheReturnValueBecomesTheExitCodeWithoutExitingTheProcess()
        {
            var cmd = Given("start").Command("start", "start it", c => c.Run(() => 3)).TryParse();

            Assert.IsFalse(cmd.ShouldExit, "a command that ran is not a command line that could not be read");
            Assert.IsTrue(cmd.HandlerRan);
            Assert.AreEqual(3, cmd.ExitCode);
        }

        [TestMethod]
        public void Run_AValueReturningMethodStillBindsToTheExitCodeOverload()
        {
            // Run(Action) and Run(Func<int>) both accept `() => Three()`. If the void one won,
            // the code would be silently dropped -- so this pins which overload C# picks.
            var cmd = Given("start").Command("start", "start it", c => c.Run(() => Three())).TryParse();

            Assert.AreEqual(3, cmd.ExitCode);
        }

        private static int Three()
        {
            return 3;
        }

        [TestMethod]
        public void Run_ACleanLineWithNoHandlerLeavesTheExitCodeAtZero()
        {
            var cmd = Given("start").Command("start", "start it", c => { }).TryParse();

            Assert.IsFalse(cmd.ShouldExit);
            Assert.IsFalse(cmd.HandlerRan);
            Assert.AreEqual(0, cmd.ExitCode);
        }

        [TestMethod]
        public void Run_TheHandlerDoesNotRunForABadCommandLine()
        {
            var ran = false;

            var cmd = Given("start")
                .Command("start", "start it", c =>
                {
                    c.Argument(out string file, "the file");
                    c.Run(() => { ran = true; });
                })
                .TryParse();

            Assert.IsTrue(cmd.ShouldExit);
            Assert.IsFalse(ran);
        }

        [TestMethod]
        public void Run_TheHandlerDoesNotRunForAValueThatWouldNotConvert()
        {
            var ran = false;

            var cmd = Given("start", "abc")
                .Command("start", "start it", c =>
                {
                    c.Argument(out int count, "how many to start");
                    c.Run(() => { ran = true; });
                })
                .TryParse();

            Assert.IsTrue(cmd.ShouldExit);
            Assert.IsFalse(ran, "a Run closure can never see a value that failed to convert");
            StringAssert.Contains(this.Errors, "demo start: <count> expects a whole number, but got 'abc'.");
        }

        [TestMethod]
        public void Run_TheHandlerDoesNotRunForHelp()
        {
            var ran = false;
            Given("start", "--help").Command("start", "start it", c => c.Run(() => { ran = true; })).TryParse();

            Assert.IsFalse(ran);
        }

        [TestMethod]
        public void Run_AnExceptionFromTheHandlerIsNotSwallowed()
        {
            Assert.Throws<InvalidTimeZoneException>(
                () => Given("start")
                    .Command("start", "start it", c => c.Run(() => { throw new InvalidTimeZoneException(); }))
                    .TryParse());
        }

        [TestMethod]
        public async Task RunAsync_IsAwaitedByTheAsyncParse()
        {
            var ran = false;

            var cmd = await Given("start")
                .Command("start", "start it", c => c.RunAsync(async () => { await Task.Yield(); ran = true; }))
                .TryParseAsync();

            Assert.IsTrue(ran);
            Assert.IsFalse(cmd.ShouldExit);
            Assert.IsTrue(cmd.HandlerRan);
        }

        [TestMethod]
        public async Task RunAsync_ItsReturnValueBecomesTheExitCode()
        {
            var cmd = await Given("start")
                .Command("start", "start it", c => c.RunAsync(async () => { await Task.Yield(); return 4; }))
                .TryParseAsync();

            Assert.AreEqual(4, cmd.ExitCode);
        }

        [TestMethod]
        public async Task RunAsync_TheAsyncParseStillRunsAPlainHandler()
        {
            var cmd = await Given("start").Command("start", "start it", c => c.Run(() => 5)).TryParseAsync();

            Assert.AreEqual(5, cmd.ExitCode);
        }

        [TestMethod]
        public void RunAsync_UnderTheSyncParseThrowsAndSaysToUseParseAsync()
        {
            // Blocking on it would be the quiet answer; the loud one is what this type is for.
            var thrown = Assert.Throws<InvalidOperationException>(
                () => Given("start")
                    .Command("start", "start it", c => c.RunAsync(async () => { await Task.Yield(); }))
                    .TryParse());

            StringAssert.Contains(thrown.Message, "ParseAsync()");
            StringAssert.Contains(thrown.Message, "demo start");
        }

        [TestMethod]
        public void Declaring_ACommandBesideAPositionalThrows()
        {
            Assert.Throws<InvalidOperationException>(
                () => Given().Argument("file", "the file").Command("start", "start it", c => { }));

            Assert.Throws<InvalidOperationException>(
                () => Given().Command("start", "start it", c => { }).Argument("file", "the file"));
        }

        [TestMethod]
        public void Declaring_ACommandAfterATypedDeclarationThrows()
        {
            // The mirror of the Rest() rule: the command word is where this level's switches
            // stop, and the typed value was already read without knowing that.
            var thrown = Assert.Throws<InvalidOperationException>(
                () => Given().Switch(out bool verbose, "say more").Command("start", "start it", c => { }));

            StringAssert.Contains(thrown.Message, "typed declaration");
        }

        [TestMethod]
        public void Declaring_TwoCommandsWithOneNameThrows()
        {
            var thrown = Assert.Throws<InvalidOperationException>(
                () => Given().Command("dry-run", "rehearse it", c => { }).Command("dryrun", "again", c => { }));

            StringAssert.Contains(thrown.Message, "collides");
        }

        [TestMethod]
        public void Declaring_ACommandAndASwitchWithOneNameThrows()
        {
            Assert.Throws<InvalidOperationException>(
                () => Given().Command("start", "start it", c => { }).Switch("start", "something else"));

            Assert.Throws<InvalidOperationException>(
                () => Given().Switch("start", "something else").Command("start", "start it", c => { }));
        }

        [TestMethod]
        public void Declaring_ACommandThatRedeclaresAGlobalThrows()
        {
            // Which --verbose you got would otherwise depend on which side of the command word
            // it was typed.
            var thrown = Assert.Throws<InvalidOperationException>(
                () => Given("start")
                    .Command("start", "start it", c => c.Switch("verbose", "say more here"))
                    .Switch("verbose", "say more")
                    .TryParse());

            StringAssert.Contains(thrown.Message, "already declared by 'demo'");
        }

        [TestMethod]
        public void Declaring_ACommandWithoutALambdaThrows()
        {
            Assert.Throws<ArgumentNullException>(() => Given().Command("start", "start it", null));
        }

        [TestMethod]
        public void Declaring_ACommandHelpThatLooksLikeAnAliasThrows()
        {
            var thrown = Assert.Throws<ArgumentException>(() => Given().Command("remove", "rm", c => { }));
            StringAssert.Contains(thrown.Message, "help text");
        }

        [TestMethod]
        public void Declaring_RunAtTheTopLevelThrows()
        {
            var thrown = Assert.Throws<InvalidOperationException>(() => Given().Run(() => { }));
            StringAssert.Contains(thrown.Message, "top level");
        }

        [TestMethod]
        public void Declaring_RunBesideCommandsThrows()
        {
            Assert.Throws<InvalidOperationException>(
                () => Given("nuget")
                    .Command("nuget", "the feed", c =>
                    {
                        c.Command("push", "push one", p => { });
                        c.Run(() => { });
                    })
                    .TryParse());

            Capture();
            Assert.Throws<InvalidOperationException>(
                () => Given("nuget")
                    .Command("nuget", "the feed", c =>
                    {
                        c.Run(() => { });
                        c.Command("push", "push one", p => { });
                    })
                    .TryParse());
        }

        [TestMethod]
        public void Declaring_TwoHandlersOnOneCommandThrows()
        {
            Assert.Throws<InvalidOperationException>(
                () => Given("start")
                    .Command("start", "start it", c =>
                    {
                        c.Run(() => { });
                        c.Run(() => { });
                    })
                    .TryParse());
        }

        [TestMethod]
        public void Declaring_ProgramInsideACommandThrows()
        {
            var thrown = Assert.Throws<InvalidOperationException>(
                () => Given("start").Command("start", "start it", c => c.Program("other")).TryParse());

            StringAssert.Contains(thrown.Message, "named by Command()");
        }
    }
}
