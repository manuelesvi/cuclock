using Aphorismus.Shared.Entities;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace CUClock.Shared.Messages;

/// <summary>
/// Encapsulates a search result with a collection of 
/// <see cref="Frase"/> entities.
/// </summary>
/// <param name="value"></param>
public class SearchResultMessage(IEnumerable<Frase> value)
    : ValueChangedMessage<IEnumerable<Frase>>(value)
{
}
