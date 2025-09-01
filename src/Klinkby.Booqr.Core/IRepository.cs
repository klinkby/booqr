namespace Klinkby.Booqr.Core;

/// <Inheritdoc />
public interface IRepository<T> : IRepository<T, int>;

/// <summary>
///     Represents the base repository interface for managing entities of type <typeparamref name="T" />
///     with a unique identifier of type <typeparamref name="TKey" />.
/// </summary>
/// <typeparam name="T">The type of the entities managed by the repository.</typeparam>
/// <typeparam name="TKey">The type of the unique identifier for entities.</typeparam>
public interface IRepository<T, TKey>
{
    /// <summary>
    ///     Retrieves all items of type <typeparamref name="T" /> from the repository based on the given paging parameters.
    /// </summary>
    /// <param name="pageQuery">The parameters specifying the starting point and number of items to retrieve.</param>
    /// <param name="cancellation">The token used to propagate notification that the operation should be canceled.</param>
    /// <returns>An asynchronous stream of items of type <typeparamref name="T" />.</returns>
    IAsyncEnumerable<T> GetAll(IPageQuery pageQuery, CancellationToken cancellation = default);

    /// <summary>
    ///     Retrieves an item of type <typeparamref name="T" /> from the repository based on the specified identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the item to retrieve.</param>
    /// <param name="cancellation">The token used to propagate notification that the operation should be canceled.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the item of type
    ///     <typeparamref name="T" /> if found, otherwise <c>null</c>.
    /// </returns>
    Task<T?> GetById(TKey id, CancellationToken cancellation = default);

    /// <summary>
    ///     Adds a new item of type <typeparamref name="T" /> to the repository, setting its Created and Modified fields,
    ///     and returns the new unique identifier.
    /// </summary>
    /// <param name="newItem">The item to be added to the repository.</param>
    /// <param name="cancellation">The token used to propagate notification that the operation should be canceled.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the unique identifier of the newly
    ///     added item.
    /// </returns>
    Task<TKey> Add(T newItem, CancellationToken cancellation = default);

    /// <summary>
    ///     Updates an existing item of type <typeparamref name="T" /> in the repository. Sets the Modified field.
    /// </summary>
    /// <param name="item">The updated item of type <typeparamref name="T" /> to be saved in the repository.</param>
    /// <param name="cancellation">The token used to propagate notification that the operation should be canceled.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<bool> Update(T item, CancellationToken cancellation = default);

    /// <summary>
    ///     Soft deletes the item of type <typeparamref name="T" /> from the repository based on the specified identifier by
    ///     setting the Deleted field.
    /// </summary>
    /// <param name="id">The unique identifier of the item to delete.</param>
    /// <param name="cancellation">The token used to propagate notification that the operation should be canceled.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<bool> Delete(TKey id, CancellationToken cancellation = default);

    Task<bool> Undelete(TKey id, CancellationToken cancellation = default);
}
