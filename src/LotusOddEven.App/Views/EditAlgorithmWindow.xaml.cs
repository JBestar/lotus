using System.Windows;
using LotusOddEven.App.ViewModels;

namespace LotusOddEven.App.Views;

public partial class EditAlgorithmWindow : Window
{
    public EditAlgorithmWindow(EditAlgorithmViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += () =>
        {
            DialogResult = viewModel.DialogResultOk;
            Close();
        };
    }
}
