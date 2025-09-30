using Aphorismus.Shared.Entities;
using CUClock.Shared.ViewModels;

namespace CUClock.Maui.Views;

public partial class Search : ContentPage
{
    public Search()
    {
        InitializeComponent();
        var vm = App.Current!.Handler.GetService<SemanticSearch>()
            ?? throw new NullReferenceException();
        BindingContext = vm;
    }

    public new SemanticSearch BindingContext
    {
        get => (SemanticSearch)base.BindingContext;
        set => base.BindingContext = value;
    }

    private void Query_Completed(object sender, EventArgs e)
    {
        BindingContext.Search.Execute(QueryEntry.Text);
    }

    private void Buscar_Clicked(object sender, EventArgs e)
    {
        BindingContext.Search.Execute(QueryEntry.Text);
    }

    private void CollectionView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = e.CurrentSelection.FirstOrDefault() as Frase;
        if (selected is null)
        {
            return;
        }
        var text = string.Format("{0}\n{1}",
            selected.Capitulo?.Nombre,
            selected.Texto);
        Clipboard.Default.SetTextAsync(text);
    }
}