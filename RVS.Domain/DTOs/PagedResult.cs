namespace RVS.Domain.DTOs
{
    public sealed record PagedResult<T>
    {
        public int Page { get; init; }
        public int PageSize { get; init; }
        public long TotalCount { get; init; }
        public List<T> Items { get; init; } = [];
        
        /// <summary>
        /// Cosmos DB continuation token for efficient pagination.
        /// Use this for next page instead of OFFSET/LIMIT.
        /// </summary>
        public string? ContinuationToken { get; init; }
    }
}
