using TbotUltra.Desktop.Common;

namespace TbotUltra.Desktop.ViewModels;

/// <summary>Owns optional post-login analysis selections.</summary>
public sealed class PostLoginSettingsViewModel : BaseViewModel
{
    private bool _analyzeFarmlists;
    private bool _analyzeHero;
    private bool _analyzeHeroInventory;
    private bool _readTroopTrainingQueue;
    private bool _analyzeBrewery;
    private bool _analyzeNewVillages = true;
    private bool _analyzeNewAccount = true;

    public bool AnalyzeFarmlists { get => _analyzeFarmlists; set => SetProperty(ref _analyzeFarmlists, value); }
    public bool AnalyzeHero { get => _analyzeHero; set => SetProperty(ref _analyzeHero, value); }
    public bool AnalyzeHeroInventory { get => _analyzeHeroInventory; set => SetProperty(ref _analyzeHeroInventory, value); }
    public bool ReadTroopTrainingQueue { get => _readTroopTrainingQueue; set => SetProperty(ref _readTroopTrainingQueue, value); }
    public bool AnalyzeBrewery { get => _analyzeBrewery; set => SetProperty(ref _analyzeBrewery, value); }
    public bool AnalyzeNewVillages { get => _analyzeNewVillages; set => SetProperty(ref _analyzeNewVillages, value); }
    public bool AnalyzeNewAccount { get => _analyzeNewAccount; set => SetProperty(ref _analyzeNewAccount, value); }
}
