using System.Text;
using System.Text.Json;

namespace AndreGoepel.Testing.E2E.Tests;

public class MailHogClientTests
{
    [Fact]
    public void TryExtractFirstLink_BodyContainsMatchingLink_ReturnsIt()
    {
        // Arrange
        var body = "Click here: https://localhost:1234/Account/ConfirmEmail?userId=abc to confirm.";

        // Act
        var link = MailHogClient.TryExtractFirstLink(body, "Account/ConfirmEmail");

        // Assert
        Assert.Equal("https://localhost:1234/Account/ConfirmEmail?userId=abc", link);
    }

    [Fact]
    public void TryExtractFirstLink_NoLinkMatchesFilter_ReturnsNull()
    {
        // Arrange
        var body = "Click here: https://localhost:1234/Account/ResetPassword?token=abc to reset.";

        // Act
        var link = MailHogClient.TryExtractFirstLink(body, "Account/ConfirmEmail");

        // Assert
        Assert.Null(link);
    }

    [Fact]
    public void TryExtractFirstLink_LinkHasHtmlEncodedAmpersand_DecodesIt()
    {
        // Arrange
        var body = "https://localhost:1234/Account/ConfirmEmail?userId=abc&amp;code=xyz";

        // Act
        var link = MailHogClient.TryExtractFirstLink(body, "Account/ConfirmEmail");

        // Assert
        Assert.Equal("https://localhost:1234/Account/ConfirmEmail?userId=abc&code=xyz", link);
    }

    [Fact]
    public void TryExtractFirstLink_MultipleLinks_ReturnsFirstMatch()
    {
        // Arrange
        var body =
            "https://localhost:1234/Account/ResetPassword?token=abc "
            + "https://localhost:1234/Account/ConfirmEmail?userId=1 "
            + "https://localhost:1234/Account/ConfirmEmail?userId=2";

        // Act
        var link = MailHogClient.TryExtractFirstLink(body, "Account/ConfirmEmail");

        // Assert
        Assert.Equal("https://localhost:1234/Account/ConfirmEmail?userId=1", link);
    }

    [Fact]
    public void IsQuotedPrintable_HeaderDeclaresQuotedPrintable_ReturnsTrue()
    {
        // Arrange
        var part = ParsePart(
            """{ "Headers": { "Content-Transfer-Encoding": ["quoted-printable"] }, "Body": "" }"""
        );

        // Act
        var result = MailHogClient.IsQuotedPrintable(part);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("""{ "Headers": { "Content-Transfer-Encoding": ["7bit"] }, "Body": "" }""")]
    [InlineData("""{ "Headers": {}, "Body": "" }""")]
    [InlineData("""{ "Body": "" }""")]
    public void IsQuotedPrintable_HeaderMissingOrNotQuotedPrintable_ReturnsFalse(string json)
    {
        // Arrange
        var part = ParsePart(json);

        // Act
        var result = MailHogClient.IsQuotedPrintable(part);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void DecodeQuotedPrintable_EncodedUrlWithSoftLineBreak_ReassemblesOriginalUrl()
    {
        // Arrange
        // MailHog wraps long lines with a soft break ("=\r\n") and encodes "?" and "=" as hex escapes.
        var encoded = "https://localhost/Account/ConfirmEmail=\r\n?userId=3Dabc&code=3Dxyz";

        // Act
        var decoded = MailHogClient.DecodeQuotedPrintable(encoded);

        // Assert
        Assert.Equal("https://localhost/Account/ConfirmEmail?userId=abc&code=xyz", decoded);
    }

    [Fact]
    public void AppendPart_PartDeclaresQuotedPrintable_AppendsDecodedBody()
    {
        // Arrange
        var builder = new StringBuilder();
        var part = ParsePart(
            """
            {
                "Headers": { "Content-Transfer-Encoding": ["quoted-printable"] },
                "Body": "userId=3Dabc"
            }
            """
        );

        // Act
        MailHogClient.AppendPart(builder, part);

        // Assert
        Assert.Equal("userId=abc" + Environment.NewLine, builder.ToString());
    }

    [Fact]
    public void AppendPart_PartHasNoTransferEncoding_AppendsBodyVerbatim()
    {
        // Arrange
        var builder = new StringBuilder();
        var part = ParsePart("""{ "Body": "userId=abc" }""");

        // Act
        MailHogClient.AppendPart(builder, part);

        // Assert
        Assert.Equal("userId=abc" + Environment.NewLine, builder.ToString());
    }

    [Fact]
    public void DecodeBody_MultipartMessage_ConcatenatesAllPartBodies()
    {
        // Arrange
        var message = ParsePart(
            """
            {
                "Content": { "Body": "plain text part" },
                "MIME": {
                    "Parts": [
                        { "Body": "first mime part" },
                        { "Body": "second mime part" }
                    ]
                }
            }
            """
        );

        // Act
        var body = MailHogClient.DecodeBody(message);

        // Assert
        Assert.Contains("plain text part", body);
        Assert.Contains("first mime part", body);
        Assert.Contains("second mime part", body);
    }

    [Fact]
    public void DecodeBody_NonMultipartMessage_UsesContentOnly()
    {
        // Arrange
        var message = ParsePart("""{ "Content": { "Body": "only part" }, "MIME": null }""");

        // Act
        var body = MailHogClient.DecodeBody(message);

        // Assert
        Assert.Equal("only part" + Environment.NewLine, body);
    }

    private static JsonElement ParsePart(string json) => JsonDocument.Parse(json).RootElement;
}
