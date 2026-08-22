using System.Windows.Controls;
using TbotUltra.Desktop.Models;
using TbotUltra.Worker.Domain;
using Xunit;

namespace TbotUltra.Desktop.Tests;

[Collection(WpfSmokeCollection.Name)]
public sealed class CreateFarmListsWindowTests(WpfSmokeFixture wpf)
{
    [Fact]
    public void OnlyLossReports_UsesSavedValueAndPersistsChangesImmediately()
    {
        wpf.Run(() =>
        {
            bool? saved = null;
            var window = new CreateFarmListsWindow(
                "Gauls",
                [new VillageSelectionItem
                {
                    Name = "Capital",
                    Url = "/dorf1.php?newdid=1",
                    Tribe = "Gauls",
                }],
                onlyCreateReportsWithLosses: true,
                onlyCreateReportsWithLossesChanged: value => saved = value,
                (_, _, _) => Task.FromResult(new FarmListCreateBatchResult(0, 0, [])),
                CancellationToken.None);

            var checkBox = Assert.IsType<CheckBox>(window.FindName("OnlyCreateReportsWithLossesCheckBox"));
            Assert.True(checkBox.IsChecked);
            Assert.Null(saved);

            checkBox.IsChecked = false;

            Assert.False(saved);
            window.Close();
        });
    }
}
