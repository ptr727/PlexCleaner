using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

public sealed class VerifyOptionsTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, false)]
    public void VerifyValues_DeleteAndRegister_FailsOnlyWhenBoth(
        bool delete,
        bool register,
        bool valid
    )
    {
        VerifyOptions2 options = new()
        {
            DeleteInvalidFiles = delete,
            RegisterInvalidFiles = register,
        };
        _ = options.VerifyValues().Should().Be(valid);
    }
}
