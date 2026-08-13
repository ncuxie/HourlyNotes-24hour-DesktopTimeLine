using System.ComponentModel;
using System.Text.Json.Serialization;

namespace HourlyNotes;

public class HourEntry : INotifyPropertyChanged
{
    public int Hour { get; set; }

    private string _text = "";
    public string Text
    {
        get => _text;
        set { _text = value ?? ""; OnPropertyChanged(nameof(Text)); }
    }

    [JsonIgnore]
    public string TimeLabel => $"{Hour:00}:00";

    private bool _isCurrent;
    [JsonIgnore]
    public bool IsCurrent
    {
        get => _isCurrent;
        set { if (_isCurrent != value) { _isCurrent = value; OnPropertyChanged(nameof(IsCurrent)); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
