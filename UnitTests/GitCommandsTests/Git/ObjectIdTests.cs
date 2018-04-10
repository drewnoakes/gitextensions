﻿using System;
using System.Text;
using GitUIPluginInterfaces;
using JetBrains.Annotations;
using NUnit.Framework;

namespace GitCommandsTests.Git
{
    // TODO SUT is in GitUIPluginInterfaces but no test assembly exists for that assembly

    [TestFixture]
    public sealed class ObjectIdTests
    {
        [TestCase("0000000000000000000000000000000000000000")]
        [TestCase("0102030405060708091011121314151617181920")]
        [TestCase("0123456789abcdef0123456789abcdef01234567")]
        public void TryParse_handles_valid_hashes(string sha1)
        {
            Assert.True(ObjectId.TryParse(sha1, out var id));
            Assert.AreEqual(sha1.ToLower(), id.ToString());
        }

        [TestCase("00000000000000000000000000000000000000")]
        [TestCase("000000000000000000000000000000000000000")]
        [TestCase("01020304050607080910111213141516171819200")]
        [TestCase("010203040506070809101112131415161718192001")]
        [TestCase("ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ")]
        [TestCase("  0000000000000000000000000000000000000000  ")]
        public void TryParse_identifies_invalid_hashes(string sha1)
        {
            Assert.False(ObjectId.TryParse(sha1, out _));
        }

        [TestCase("0000000000000000000000000000000000000000", 0)]
        [TestCase("0000000000000000000000000000000000000000__", 0)]
        [TestCase("_0102030405060708091011121314151617181920", 1)]
        [TestCase("_0102030405060708091011121314151617181920_", 1)]
        [TestCase("__0102030405060708091011121314151617181920", 2)]
        [TestCase("__0102030405060708091011121314151617181920__", 2)]
        public void TryParse_with_offset_handles_valid_hashes(string sha1, int offset)
        {
            Assert.True(ObjectId.TryParse(sha1, offset, out var id));
            Assert.AreEqual(
                sha1.Substring(offset, 40),
                id.ToString());
        }

        [TestCase("0000000000000000000000000000000000000000")]
        [TestCase("0102030405060708091011121314151617181920")]
        [TestCase("0123456789abcdef0123456789abcdef01234567")]
        public void Parse_handles_valid_hashes(string sha1)
        {
            Assert.AreEqual(
                sha1.ToLower(),
                ObjectId.Parse(sha1).ToString());
        }

        [TestCase("00000000000000000000000000000000000000")]
        [TestCase("000000000000000000000000000000000000000")]
        [TestCase("01020304050607080910111213141516171819200")]
        [TestCase("010203040506070809101112131415161718192001")]
        [TestCase("ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ")]
        [TestCase("  0000000000000000000000000000000000000000  ")]
        public void Parse_throws_for_invalid_hashes(string sha1)
        {
            Assert.Throws<FormatException>(() => ObjectId.Parse(sha1));
        }

        [TestCase("0000000000000000000000000000000000000000")]
        [TestCase("0102030405060708091011121314151617181920")]
        [TestCase("0123456789abcdef0123456789abcdef01234567")]
        public void IsValid_identifies_valid_hashes(string sha1)
        {
            Assert.True(ObjectId.IsValid(sha1));
        }

        [TestCase("00000000000000000000000000000000000000")]
        [TestCase("000000000000000000000000000000000000000")]
        [TestCase("01020304050607080910111213141516171819200")]
        [TestCase("010203040506070809101112131415161718192001")]
        [TestCase("ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ")]
        [TestCase("  0000000000000000000000000000000000000000  ")]
        public void IsValid_identifies_invalid_hashes(string sha1)
        {
            Assert.False(ObjectId.IsValid(sha1));
        }

        [TestCase("0000000000000000000000000000000000000000", 0)]
        [TestCase("0000000000000000000000000000000000000000__", 0)]
        [TestCase("_0102030405060708091011121314151617181920", 1)]
        [TestCase("_0102030405060708091011121314151617181920_", 1)]
        [TestCase("__0102030405060708091011121314151617181920", 2)]
        [TestCase("__0102030405060708091011121314151617181920__", 2)]
        public void Parse_with_offset_handles_valid_hashes(string sha1, int offset)
        {
            Assert.AreEqual(
                sha1.Substring(offset, 40),
                ObjectId.Parse(sha1, offset).ToString());
        }

        [Test]
        public void UnstagedId_has_expected_value()
        {
            Assert.AreEqual(
                "1111111111111111111111111111111111111111",
                ObjectId.UnstagedId.ToString());
        }

        [Test]
        public void IndexId_has_expected_value()
        {
            Assert.AreEqual(
                "2222222222222222222222222222222222222222",
                ObjectId.IndexId.ToString());
        }

        [Test]
        public void UnstagedId_is_artificial()
        {
            Assert.IsTrue(ObjectId.UnstagedId.IsArtificial);
        }

        [Test]
        public void IndexId_is_artificial()
        {
            Assert.IsTrue(ObjectId.IndexId.IsArtificial);
        }

        [Test]
        public void Equivalent_ids_are_equal()
        {
            Assert.AreEqual(
                ObjectId.Parse("0102030405060708091011121314151617181920"),
                ObjectId.Parse("0102030405060708091011121314151617181920"));

            Assert.AreEqual(
                ObjectId.Parse("abcdefabcdefabcdefabcdefabcdefabcdefabcd"),
                ObjectId.Parse("abcdefabcdefabcdefabcdefabcdefabcdefabcd"));

            Assert.AreEqual(
                ObjectId.UnstagedId,
                ObjectId.UnstagedId);

            Assert.AreEqual(
                ObjectId.IndexId,
                ObjectId.IndexId);
        }

        [Test]
        public void Different_ids_are_not_equal()
        {
            Assert.AreNotEqual(
                ObjectId.Parse("0000000000000000000000000000000000000000"),
                ObjectId.Parse("0102030405060708091011121314151617181920"));

            Assert.AreNotEqual(
                ObjectId.UnstagedId,
                ObjectId.IndexId);
        }

        [Test]
        public void Equivalent_ids_have_equal_hash_codes()
        {
            Assert.AreEqual(
                ObjectId.Parse("0102030405060708091011121314151617181920").GetHashCode(),
                ObjectId.Parse("0102030405060708091011121314151617181920").GetHashCode());

            Assert.AreEqual(
                ObjectId.Parse("abcdefabcdefabcdefabcdefabcdefabcdefabcd").GetHashCode(),
                ObjectId.Parse("abcdefabcdefabcdefabcdefabcdefabcdefabcd").GetHashCode());

            Assert.AreEqual(
                ObjectId.UnstagedId.GetHashCode(),
                ObjectId.UnstagedId.GetHashCode());

            Assert.AreEqual(
                ObjectId.IndexId.GetHashCode(),
                ObjectId.IndexId.GetHashCode());
        }

        [Test]
        public void Different_ids_have_different_hash_codes()
        {
            Assert.AreNotEqual(
                ObjectId.Parse("0000000000000000000000000000000000000000").GetHashCode(),
                ObjectId.Parse("0102030405060708091011121314151617181920").GetHashCode());

            Assert.AreNotEqual(
                ObjectId.UnstagedId.GetHashCode(),
                ObjectId.IndexId.GetHashCode());
        }

        private const string NonHexAscii = "0123456789abcdefghijklmnopqrstuvwxyz";
        private const string HexAscii = "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f20";

        [TestCase(HexAscii, 0, "000102030405060708090a0b0c0d0e0f10111213")]
        [TestCase(HexAscii, 1, "00102030405060708090a0b0c0d0e0f101112131")]
        [TestCase(HexAscii, 2, "0102030405060708090a0b0c0d0e0f1011121314")]
        [TestCase(HexAscii, 3, "102030405060708090a0b0c0d0e0f10111213141")]
        [TestCase(HexAscii, 26, "0d0e0f101112131415161718191a1b1c1d1e1f20")]
        [TestCase(HexAscii, 27, null)]
        [TestCase(HexAscii, -1, null)]
        [TestCase(NonHexAscii, 0, null)]
        public void TryParseAsciiHexBytes_works_as_expected(string source, int offset, [CanBeNull] string expected)
        {
            var sourceBytes = Encoding.ASCII.GetBytes(source);

            Assert.AreEqual(expected != null, ObjectId.TryParseAsciiHexBytes(sourceBytes, offset, out var id));

            if (expected != null)
            {
                Assert.AreEqual(ObjectId.Parse(expected), id);
            }
        }
    }
}