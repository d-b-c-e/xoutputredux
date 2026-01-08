using XOutputRenew.Core.Configuration;

namespace XOutputRenew.Tests;

[TestClass]
public class ProfileManagerTests
{
    [TestMethod]
    public void GetProfileNames_WhenNoProfiles_ReturnsEmpty()
    {
        // Arrange
        var manager = new ProfileManager();

        // Act
        var names = manager.GetProfileNames();

        // Assert - should at least not throw
        Assert.IsNotNull(names);
    }

    [TestMethod]
    public void Profile_CanBeCreated()
    {
        // Arrange & Act
        var profile = new Profile
        {
            Name = "TestProfile",
            Description = "Test description"
        };

        // Assert
        Assert.AreEqual("TestProfile", profile.Name);
        Assert.AreEqual("Test description", profile.Description);
        Assert.IsNotNull(profile.ButtonMappings);
        Assert.IsNotNull(profile.AxisMappings);
    }
}
