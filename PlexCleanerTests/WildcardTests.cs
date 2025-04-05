﻿using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

public class WildcardTests(PlexCleanerTests fixture) : IClassFixture<PlexCleanerTests>
{
#pragma warning disable IDE0052 // Remove unread private members
    private readonly PlexCleanerTests _fixture = fixture;
#pragma warning restore IDE0052 // Remove unread private members

    [Theory]
    [InlineData("*.fuse_hidden*", "Foo.fuse_hidden1234", true)]
    [InlineData("*.fuse_hidden*", "fuse_hidden.foo", false)]
    [InlineData("*.partial~", "Foo.partial~", true)]
    [InlineData("*.partial~", "partial~.foo", false)]
    [InlineData("*.sample.*", "Foo.sample.foo", true)]
    [InlineData("*.sample.*", "sample.foo", false)]
    [InlineData("*.sample.*", "foo.sample", false)]
    public void WildcardMatch(string wildCard, string fileName, bool match) => Assert.Equal(match, ProcessOptions4.MaskToRegex(wildCard).IsMatch(fileName));
}
