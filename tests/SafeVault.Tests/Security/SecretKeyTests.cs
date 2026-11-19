using SafeVault.Security;

namespace SafeVault.Tests.Security;

public class SecretKeyTests
{
    [Fact]
    public void Generate_ProduitUneCleDe32Octets()
    {
        using var key = SecretKey.Generate();
        Assert.Equal(SecretKey.SizeInBytes, key.Span.Length);
    }

    [Fact]
    public void Generate_ProduitDesClesDifferentes()
    {
        using var a = SecretKey.Generate();
        using var b = SecretKey.Generate();
        Assert.False(a.Span.SequenceEqual(b.Span));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    [InlineData(31)]
    [InlineData(33)]
    public void FromBytes_RefuseUneTailleIncorrecte(int size)
    {
        Assert.Throws<ArgumentException>(() => SecretKey.FromBytes(new byte[size]));
    }

    [Fact]
    public void Dispose_EffaceLaCleEnMemoire()
    {
        var raw = Enumerable.Repeat((byte)0xAB, SecretKey.SizeInBytes).ToArray();
        var key = SecretKey.TakeOwnership(raw);

        key.Dispose();

        Assert.True(key.IsDisposed);
        Assert.All(raw, b => Assert.Equal(0, b));
    }

    [Fact]
    public void Span_ApresDispose_LeveUneException()
    {
        var key = SecretKey.Generate();
        key.Dispose();
        Assert.Throws<ObjectDisposedException>(() => key.Span.Length);
    }
}
