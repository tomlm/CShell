using CShellNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace CShellLibTests
{
    /// <summary>
    /// What AsJson() hands back: JSON walked with a dot, and still a JsonNode underneath.
    /// </summary>
    [TestClass]
    public class JsonDynamicTests
    {
        private const string Sample = @"{
            ""name"": ""Joe Smith"",
            ""age"": 42,
            ""active"": true,
            ""missingIsNull"": null,
            ""owner"": { ""login"": ""tomlm"" },
            ""tags"": [ ""a"", ""b"" ],
            ""repos"": [ { ""name"": ""CShell"" }, { ""name"": ""scripts"" } ]
        }";

        private static dynamic Json() => new JsonDynamic(JsonNode.Parse(Sample));

        [TestMethod]
        public void Member_ReadsAProperty()
        {
            Assert.AreEqual("Joe Smith", (string)Json().name);
        }

        [TestMethod]
        public void Member_NestsAsDeepAsYouLike()
        {
            Assert.AreEqual("tomlm", (string)Json().owner.login);
            Assert.AreEqual("scripts", (string)Json().repos[1].name);
        }

        [TestMethod]
        public void Member_ThatIsNotThereReadsAsNull()
        {
            // What makes `if (json.optional != null)` the way to test for a field. The cost is
            // that a typo reads as null too.
            Assert.IsNull(Json().nope);
            Assert.IsNull(Json().missingIsNull);
        }

        [TestMethod]
        public void Indexer_WorksOnObjectsAndArrays()
        {
            Assert.AreEqual("tomlm", (string)Json()["owner"]["login"]);
            Assert.AreEqual("b", (string)Json().tags[1]);
        }

        [TestMethod]
        public void Indexer_OutOfRangeReadsAsNull()
        {
            Assert.IsNull(Json().tags[99]);
        }

        [TestMethod]
        public void Convert_AssignsToTypedVariables()
        {
            string name = Json().name;
            int age = Json().age;
            bool active = Json().active;

            Assert.AreEqual("Joe Smith", name);
            Assert.AreEqual(42, age);
            Assert.IsTrue(active);
        }

        [TestMethod]
        public void Convert_GivesBackTheJsonNodeItself()
        {
            // The escape hatch to the typed API, and the reason a wrapper is enough even though
            // JsonObject is sealed and cannot be derived from.
            JsonNode node = Json().owner;
            Assert.AreEqual("tomlm", (string)node["login"]);

            JsonObject o = Json();
            Assert.AreEqual("Joe Smith", (string)o["name"]);

            JsonArray a = Json().tags;
            Assert.AreEqual(2, a.Count);
        }

        [TestMethod]
        public void Convert_DeserializesAWholeShape()
        {
            var owner = (Owner)Json().owner;
            Assert.AreEqual("tomlm", owner.Login);
        }

        [TestMethod]
        public void Operators_CompareAndArithmetic()
        {
            // DynamicObject binds members and conversions but not operators; without
            // TryBinaryOperation every one of these throws RuntimeBinderException.
            Assert.IsTrue(Json().age == 42);
            Assert.IsTrue(Json().age != 7);
            Assert.IsTrue(Json().age > 41);
            Assert.IsTrue(Json().age <= 42);
            Assert.IsTrue(Json().name == "Joe Smith");
            Assert.AreEqual(43, Json().age + 1);
        }

        [TestMethod]
        public void Enumeration_WalksAnArray()
        {
            var seen = new List<string>();
            foreach (var tag in Json().tags)
            {
                seen.Add((string)tag);
            }

            CollectionAssert.AreEqual(new[] { "a", "b" }, seen);

            var names = new List<string>();
            foreach (var repo in Json().repos)
            {
                names.Add((string)repo.name);
            }

            CollectionAssert.AreEqual(new[] { "CShell", "scripts" }, names);
        }

        [TestMethod]
        public void Enumeration_OfSomethingThatIsNotAnArrayIsEmpty()
        {
            var count = 0;
            foreach (var nothing in Json().owner)
            {
                count++;
            }

            Assert.AreEqual(0, count);
        }

        [TestMethod]
        public void MemberNames_AreDiscoverable()
        {
            var names = (IEnumerable<string>)((JsonDynamic)Json()).GetDynamicMemberNames();

            CollectionAssert.Contains(new List<string>(names), "owner");
        }

        [TestMethod]
        public void ToString_GivesTheValueWithoutItsQuotes()
        {
            Assert.AreEqual("Joe Smith", Json().name.ToString());
        }

        [TestMethod]
        public void WrappingNullIsSafe()
        {
            var empty = new JsonDynamic(null);

            Assert.IsNull(empty.Node);
            Assert.AreEqual(String.Empty, empty.ToString());
            Assert.IsNull((JsonNode)empty);
        }

        public class Owner
        {
            public string Login { get; set; }
        }
    }
}
