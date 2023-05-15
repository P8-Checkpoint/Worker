namespace p8WorkerTest;

public class SanityCheck
{
    [Fact]
    public void SanityChecks()
    {
        var actual = 1 + 1;
        Assert.Equal(2, actual);
    }
}
