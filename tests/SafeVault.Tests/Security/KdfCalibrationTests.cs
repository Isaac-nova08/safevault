using SafeVault.Security;

namespace SafeVault.Tests.Security;

public class KdfCalibrationTests
{
    [Fact]
    public void ObjectifTresCourt_NeDescendJamaisSousLesParametresParDefaut()
    {
        var kdf = KdfCalibration.Calibrate(TimeSpan.FromMilliseconds(1));

        Assert.Equal(KdfParameters.Default, kdf);
    }

    [Fact]
    public void ObjectifTresLong_EstPlafonne()
    {
        var kdf = KdfCalibration.Calibrate(TimeSpan.FromHours(1));

        Assert.Equal(KdfCalibration.MaxCalibratedIterations, kdf.Iterations);
        Assert.Equal(KdfParameters.Default.MemoryKiB, kdf.MemoryKiB);
        Assert.Equal(KdfParameters.Default.Parallelism, kdf.Parallelism);
    }

    [Fact]
    public void ParametresCalibres_SontValides()
    {
        var kdf = KdfCalibration.ForThisMachine;

        kdf.Validate();
        Assert.InRange(kdf.Iterations, KdfParameters.Default.Iterations, KdfCalibration.MaxCalibratedIterations);
    }
}
