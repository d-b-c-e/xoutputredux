using System.ComponentModel;
using System.Runtime.CompilerServices;
using XOutputRenew.Core.Mapping;

namespace XOutputRenew.App.ViewModels;

/// <summary>
/// View model for displaying a mapping profile.
/// </summary>
public class ProfileViewModel : INotifyPropertyChanged
{
    private string _status = "Stopped";
    private bool _isRunning;

    public string FileName { get; }
    public string Name => Profile.Name;
    public string? Description => Profile.Description;
    public int TotalBindings => Profile.TotalBindings;
    public MappingProfile Profile { get; }

    public string Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (_isRunning != value)
            {
                _isRunning = value;
                Status = value ? "Running" : "Stopped";
                OnPropertyChanged();
            }
        }
    }

    public ProfileViewModel(string fileName, MappingProfile profile)
    {
        FileName = fileName;
        Profile = profile;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
