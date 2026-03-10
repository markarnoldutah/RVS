# Cosmos DB Integrated Cache - Benefit & Cost Savings Analysis

## Executive Summary

Based on the BF.API healthcare SaaS platform modeling and access patterns, **Azure Cosmos DB Integrated Cache will deliver significant cost savings and performance improvements** when implemented at the right scale.

### Key Findings

| Metric | Without Cache | With Integrated Cache | Savings |
|--------|---------------|----------------------|---------|
| **Year 3 RU/s Cost** | $1,112/month | $848/month | **24% reduction** |
| **Read Latency (P95)** | 8-12ms | 2-4ms | **70% improvement** |
| **Cache Hit Rate (projected)** | N/A | 85-95% | High repeat access |
| **Monthly Cache Cost** | $0 | $150/month | Small incremental cost |
| **Net Monthly Savings** | N/A | **$114/month** | 10% total reduction |
| **Annual Savings (Year 3)** | N/A | **$1,368/year** | Strong ROI |

### Implementation Timeline

**Recommendation**: ? **IMPLEMENT Integrated Cache at Year 2 (200+ practices)** when cost savings justify the cache expense.

---

## Table of Contents

1. [Access Pattern Analysis](#access-pattern-analysis)
2. [Cost-Benefit Analysis](#cost-benefit-analysis)
3. [Performance Benefits](#performance-benefits)
4. [Scaling Projections](#scaling-projections)
5. [Implementation Recommendations](#implementation-recommendations)
6. [Risk Assessment](#risk-assessment)
7. [Alternative Caching Strategies](#alternative-caching-strategies)
8. [Summary & Final Recommendation](#summary--final-recommendation)

---

## Access Pattern Analysis

### Cache-Friendly Characteristics

#### 1. **High Read-to-Write Ratio**

```
Daily Operations Per Practice (10 OD practice):
- Patient searches: 200/day
- Patient reads (full details): 250/day
- Patient writes (new/update): 100/day
- Encounter reads (history): 50/day
- Encounter writes (new): 150/day
- Tenant config reads: 500/day
- Payer catalog reads: 100/day

READ:WRITE Ratio = 1,100 reads : 250 writes = 4.4:1
```

**Cache Impact**: High read ratio means most requests can be served from cache, avoiding RU charges.

#### 2. **Repeat Access Patterns**

| Access Pattern | Repeat Frequency | Cache Hit Potential |
|----------------|------------------|---------------------|
| **Tenant config reads** | Every API request (0.1 RPS) | **95% hit rate** - same config accessed constantly |
| **Patient lookups** | Same patients seen annually | **60% hit rate** - returning patients |
| **Payer catalog** | Static catalog, infrequent changes | **90% hit rate** - read-mostly data |
| **Practice metadata** | Same practice context per session | **85% hit rate** - session-scoped |
| **Lookup sets** | Static reference data | **95% hit rate** - rarely changes |

#### 3. **Small Working Set**

```
Typical Practice Active Working Set (per day):
- Active patients seen today: 150-250 patients × 124KB = 18-31MB
- Tenant config: 1 document × 10KB = 10KB
- Practice metadata: 1 document × 2KB = 2KB
- Payer catalog: 100 payers × 3KB = 300KB
- Lookup sets: 20 sets × 5KB = 100KB

Total working set per practice per day: ~20-32MB
```

**Cache Impact**: Small working set fits entirely in Integrated Cache (supports up to 10GB per node).

#### 4. **Bounded Document Sizes**

| Document Type | Avg Size | Max Size | Cache Suitability |
|---------------|----------|----------|-------------------|
| Patient (with encounters) | 124KB | 304KB | ? Excellent |
| Tenant config | 10KB | 20KB | ? Excellent |
| Practice | 2KB | 5KB | ? Excellent |
| Payer | 3KB | 10KB | ? Excellent |
| Lookup set | 5KB | 15KB | ? Excellent |

**Cache Impact**: All documents are well under the 1MB cache item limit.

---

## Cost-Benefit Analysis

### Year 3 Platform Scale (500 practices)

#### Without Integrated Cache

```
Read Operations (per second, platform-wide):
- Patient searches: 500 practices × 0.01 RPS avg = 5 RPS @ 3 RU = 15 RU/s
- Patient reads: 500 × 0.01 RPS = 5 RPS @ 1 RU = 5 RU/s
- Encounter history: 500 × 0.002 RPS = 1 RPS @ 1 RU = 1 RU/s
- Tenant config: 500 × 0.02 RPS = 10 RPS @ 1 RU = 10 RU/s
- Payer catalog: 500 × 0.01 RPS = 5 RPS @ 2 RU = 10 RU/s
- Lookup sets: 500 × 0.01 RPS = 5 RPS @ 2 RU = 10 RU/s

Total Read RU/s: 51 RU/s

Write Operations (per second):
- Patient writes: 500 × 0.006 RPS = 3 RPS @ 15 RU = 45 RU/s
- Encounter writes: 500 × 0.01 RPS = 5 RPS @ 15 RU = 75 RU/s

Total Write RU/s: 120 RU/s

Total Platform RU/s Required: 171 RU/s

Monthly Cost (Serverless - $0.25 per 100K RU):
- Reads: 51 RU/s × 2.6M seconds/month = 132.6M RU = $332/month
- Writes: 120 RU/s × 2.6M seconds/month = 312M RU = $780/month
- Total: $1,112/month
```

#### With Integrated Cache

```
Cache Configuration:
- Dedicated Gateway nodes: 2 nodes (HA)
- Cache size per node: 4GB
- Total cache capacity: 8GB
- Cost: $75/node/month × 2 = $150/month

Cache Hit Rates (conservative estimates):
- Tenant config reads: 95% (mostly same config)
- Payer catalog reads: 90% (static catalog)
- Lookup set reads: 95% (static reference)
- Patient reads (returning patients): 60%
- Practice metadata: 85%

Effective RU/s After Cache:
- Patient searches: 5 RPS @ 40% cache miss = 2 RPS @ 3 RU = 6 RU/s
- Patient reads: 5 RPS @ 40% miss = 2 RPS @ 1 RU = 2 RU/s
- Encounter history: 1 RPS @ 40% miss = 0.4 RPS @ 1 RU = 0.4 RU/s
- Tenant config: 10 RPS @ 5% miss = 0.5 RPS @ 1 RU = 0.5 RU/s
- Payer catalog: 5 RPS @ 10% miss = 0.5 RPS @ 2 RU = 1 RU/s
- Lookup sets: 5 RPS @ 5% miss = 0.25 RPS @ 2 RU = 0.5 RU/s

Total Read RU/s with Cache: 10.4 RU/s (80% reduction)
Total Write RU/s (unchanged): 120 RU/s

Total Platform RU/s: 130.4 RU/s

Monthly Cosmos Cost (Serverless):
- Reads: 10.4 RU/s × 2.6M = 27M RU = $68/month
- Writes: 120 RU/s × 2.6M = 312M RU = $780/month
- Subtotal: $848/month

Monthly Cache Cost: $150/month

Total Monthly Cost: $998/month

Monthly Savings: $1,112 - $998 = $114/month
Annual Savings: $1,368/year
```

### Peak Load Considerations

During peak hours (7:30-10:30am), RPS is **3-5x higher**, which affects both serverless consumption and provisioned throughput requirements.

```
Peak Read Operations (platform-wide, 50 large practices in morning rush):
- Patient searches: 50 × 0.05 RPS peak = 2.5 RPS @ 3 RU = 7.5 RU/s
- Patient reads: 50 × 0.05 RPS = 2.5 RPS @ 1 RU = 2.5 RU/s
- Encounter history: 50 × 0.01 RPS = 0.5 RPS @ 1 RU = 0.5 RU/s
- Tenant config: 50 × 0.1 RPS = 5 RPS @ 1 RU = 5 RU/s
- Payer catalog: 50 × 0.05 RPS = 2.5 RPS @ 2 RU = 5 RU/s
- Lookup sets: 50 × 0.05 RPS = 2.5 RPS @ 2 RU = 5 RU/s

Peak Read RU/s: 25.5 RU/s (5x average due to morning rush)

Peak Write Operations:
- Patient writes: 50 × 0.006 RPS = 0.3 RPS @ 15 RU = 4.5 RU/s
- Encounter writes: 50 × 0.05 RPS = 2.5 RPS @ 15 RU = 37.5 RU/s

Peak Write RU/s: 42 RU/s

Peak Total RU/s: 67.5 RU/s (25.5 read + 42 write)
```

**With Cache Impact on Peak Load**:
- Peak Read RU/s: 25.5 @ 80% cache hit = **5.1 RU/s**
- Peak Write RU/s: 42 RU/s (unchanged)
- Peak Total: **47.1 RU/s**

**Benefit**: Cache absorbs peak traffic, preventing throttling and maintaining consistent performance.

---

## Performance Benefits

### Latency Improvements

| Operation | Without Cache (P95) | With Cache (P95) | Improvement |
|-----------|--------------------:|----------------:|-----------:|
| Patient search | 10-15ms | 2-4ms | **75%** |
| Get patient | 5-8ms | 1-2ms | **78%** |
| Tenant config | 5-7ms | 1-2ms | **80%** |
| Payer catalog | 8-12ms | 2-3ms | **79%** |
| Lookup sets | 5-8ms | 1-2ms | **80%** |

**User Experience Impact**:
- Page load times reduced by 200-500ms
- Check-in workflow feels more responsive
- Reduced perceived latency during peak hours
- More consistent performance across geographic regions

### Throughput Benefits

**Without Cache** (Serverless throttling risk):
- Burst traffic (morning rush) can approach RU/s limits
- Requests may be throttled (429 errors) during unexpected spikes
- Retry logic adds latency

**With Cache**:
- Cache absorbs 80-95% of read traffic
- Backend RU/s consumption stays well below limits
- No throttling risk during normal operations
- Consistent performance during peaks

---

## Scaling Projections

### Year 1 (50 practices)

```
Average RU/s: 17 read + 12 write = 29 RU/s
Peak RU/s: 6.8 read + 4.2 write = 11 RU/s

Without Cache:
- Serverless: $111/month (read) + $78/month (write) = $189/month

With Cache:
- Serverless: $22/month (read, 80% cached) + $78/month (write) = $100/month
- Cache: $150/month
- Total: $250/month

Year 1 Verdict: Cache costs MORE than it saves ($61/month loss)
```

**Recommendation for Year 1**: ? **DELAY cache implementation** until scale justifies cost.

### Year 2 (200 practices)

```
Average RU/s: 20.4 read + 48 write = 68.4 RU/s
Peak RU/s: 10.2 read + 16.8 write = 27 RU/s

Without Cache:
- Serverless: $445/month (read) + $312/month (write) = $757/month

With Cache:
- Serverless: $89/month (read) + $312/month (write) = $401/month
- Cache: $150/month
- Total: $551/month

Year 2 Savings: $206/month = $2,472/year
```

**Recommendation for Year 2**: ? **IMPLEMENT cache** when practice count reaches 150-200.

### Year 3+ (500 practices)

See detailed analysis above: **$114/month savings** with serverless model.

### ROI Breakeven Analysis

| Practice Count | Monthly Cosmos Cost | Cache Justification | Monthly Savings |
|----------------|--------------------:|--------------------:|----------------:|
| 50 | $189 | ? Not justified | -$61 (loss) |
| 100 | $378 | ?? Break-even point | ~$0 |
| 150 | $567 | ? Justified | +$67 |
| 200 | $757 | ? Strongly justified | +$206 |
| 500 | $1,112 | ? Strongly justified | +$114 |

---

## Implementation Recommendations

### 1. **Phased Rollout**

**Phase 1 (Year 1 - 50 practices)**:
- ? Do NOT enable Integrated Cache yet
- Monitor cache hit potential with Application Insights
- Track repeat access patterns
- Establish baseline costs
- Set up monitoring for cache readiness

**Phase 2 (Year 2 - 200+ practices)**:
- ? Enable Integrated Cache when practice count reaches 150-200
- Start with 2 dedicated gateway nodes (4GB cache each)
- Configure TTL based on data type
- Monitor hit rates and optimize

**Phase 3 (Year 3+ optimization)**:
- Monitor cache hit rates
- Adjust cache size if needed (scale to 8GB or 16GB nodes)
- Optimize TTL settings based on observed patterns
- Consider upgrading to larger nodes if eviction rate >10%

### 2. **Cache Configuration Strategy**

#### Dedicated Gateway Node Sizing

```
Recommended Configuration (Year 2+):
- Nodes: 2 (high availability)
- Size: D4s v3 (4 vCPU, 16GB RAM, 4GB cache per node)
- Total cache: 8GB
- Cost: $150/month ($75/node)

Working Set Fit Analysis:
- 200 practices × 32MB working set = 6.4GB total
- Fits comfortably in 8GB cache
- Leaves 1.6GB for overflow and other containers
```

#### TTL (Time-to-Live) Settings

| Data Type | TTL | Rationale |
|-----------|-----|-----------|
| **Tenant config** | 5 minutes | Changes infrequent, high read volume |
| **Payer catalog** | 1 hour | Static catalog, rare updates |
| **Lookup sets** | 1 hour | Static reference data |
| **Patient records** | 2 minutes | Balance between freshness and cache hits |
| **Practice metadata** | 10 minutes | Rarely changes mid-session |

**Configuration Example** (SDK):

```csharp
var clientOptions = new CosmosClientOptions
{
    ConnectionMode = ConnectionMode.Gateway,
    GatewayModeMaxConnectionLimit = 50,
    ApplicationName = "BF.API",
    
    // Enable Integrated Cache
    EnableContentResponseOnWrite = false, // Reduce RUs on writes
    ConsistencyLevel = ConsistencyLevel.Session,
    
    // Point to dedicated gateway endpoint
    GatewayEndpoint = "https://bf-cosmos-gateway.documents.azure.com:443/"
};

// Set cache TTL per request
var requestOptions = new ItemRequestOptions
{
    ConsistencyLevel = ConsistencyLevel.Session,
    
    // Integrated Cache TTL (in seconds)
    DedicatedGatewayRequestOptions = new DedicatedGatewayRequestOptions
    {
        MaxIntegratedCacheStaleness = TimeSpan.FromMinutes(5) // Tenant config
    }
};
```

#### Per-Container TTL Strategy

```csharp
public static class CacheTtlSettings
{
    // High-frequency, rarely-changing data
    public static readonly TimeSpan TenantConfig = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan PayerCatalog = TimeSpan.FromHours(1);
    public static readonly TimeSpan LookupSets = TimeSpan.FromHours(1);
    
    // Medium-frequency, session-scoped data
    public static readonly TimeSpan PracticeMetadata = TimeSpan.FromMinutes(10);
    
    // High-frequency, fresher data needed
    public static readonly TimeSpan PatientData = TimeSpan.FromMinutes(2);
    
    // No cache for writes
    public static readonly TimeSpan? NoCache = null;
}
```

### 3. **Monitoring & Optimization**

**Key Metrics to Track**:

```
Azure Monitor Metrics:
- IntegratedCacheHitRate (target: 85-95%)
- IntegratedCacheMissRate
- IntegratedCacheEvictionRate (should be low, <5%)
- TotalRequestUnits (should decrease 50-80% on reads)
- DedicatedGatewayRequests
- DedicatedGatewayAverageDuration

Application Insights:
- Cache hit rate per container
- P95 latency before/after cache
- RU consumption per operation type
- Cache-related errors or timeouts
```

**Optimization Triggers**:

| Metric | Threshold | Action |
|--------|-----------|--------|
| Cache hit rate | <75% | Increase TTL or cache size |
| Eviction rate | >10% | Increase cache size (upgrade to 8GB or 16GB nodes) |
| Read RU/s | Not decreasing | Review access patterns, adjust TTL |
| P95 latency | >5ms | Check for cache misses, network issues |
| Gateway errors | >1% | Check node health, consider failover |

### 4. **Application Code Changes**

**Minimal changes required** - Integrated Cache is transparent to most application code.

#### Basic Implementation

```csharp
// Before (direct Cosmos client):
var patient = await _container.ReadItemAsync<Patient>(
    patientId, 
    new PartitionKey(practiceId));

// After (with Integrated Cache - same code with added options):
var patient = await _container.ReadItemAsync<Patient>(
    patientId, 
    new PartitionKey(practiceId),
    new ItemRequestOptions 
    { 
        DedicatedGatewayRequestOptions = new()
        {
            MaxIntegratedCacheStaleness = CacheTtlSettings.PatientData
        }
    });
```

#### Service Layer Helper

```csharp
public class CosmosReadOptions
{
    public static ItemRequestOptions WithCache(TimeSpan ttl) => new()
    {
        DedicatedGatewayRequestOptions = new()
        {
            MaxIntegratedCacheStaleness = ttl
        }
    };
    
    public static ItemRequestOptions NoCache() => new()
    {
        DedicatedGatewayRequestOptions = null
    };
}

// Usage:
var patient = await _container.ReadItemAsync<Patient>(
    patientId, 
    new PartitionKey(practiceId),
    CosmosReadOptions.WithCache(CacheTtlSettings.PatientData));
```

#### Fallback Strategy

```csharp
// Implement graceful degradation
try
{
    // Try dedicated gateway (with cache)
    return await ReadWithCacheAsync(patientId, practiceId);
}
catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.ServiceUnavailable)
{
    // Fallback to direct endpoint (no cache)
    _logger.LogWarning("Cache gateway unavailable, falling back to direct endpoint");
    return await ReadDirectAsync(patientId, practiceId);
}
catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.RequestTimeout)
{
    // Retry with direct endpoint on timeout
    _logger.LogWarning("Cache gateway timeout, retrying with direct endpoint");
    return await ReadDirectAsync(patientId, practiceId);
}
```

#### Cache Invalidation Pattern

```csharp
// On write operations, ensure cache is bypassed
public async Task<Patient> UpdatePatientAsync(Patient patient)
{
    // Write without cache
    var response = await _container.ReplaceItemAsync(
        patient,
        patient.Id,
        new PartitionKey(patient.PracticeId),
        new ItemRequestOptions
        {
            EnableContentResponseOnWrite = false, // Save RUs
            DedicatedGatewayRequestOptions = null // Bypass cache
        });
    
    // Cache will naturally expire based on TTL
    // Or force immediate refresh on next read if needed
    return response.Resource;
}
```

---

## Risk Assessment

### Potential Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| **Cache cost exceeds savings** (Year 1) | High | Medium | Delay implementation until Year 2 (200+ practices) |
| **Cache staleness issues** | Low | Medium | Use appropriate TTL (2-5 min for patient data), invalidate on writes |
| **Gateway node failure** | Low | Low | Use 2 nodes for HA, auto-failover to direct endpoint |
| **Working set exceeds cache size** | Low | Low | Monitor eviction rate, upgrade to 8GB or 16GB nodes |
| **Wrong data cached** | Low | High | Never cache write operations, only reads |
| **Network latency to gateway** | Low | Low | Gateway in same region as Cosmos DB |

### Mitigation Strategies

**1. Cost Control**:
- Start with Year 2 when ROI is positive
- Monitor monthly spend with Azure Cost Management
- Set up budget alerts at $175/month threshold
- Disable cache if savings don't materialize after 3 months
- Use serverless pricing (avoids over-provisioning)

**2. Data Freshness**:
- Set TTL conservatively (2-5 minutes for patient data)
- Invalidate cache on writes (bypass cache for all write operations)
- Accept eventual consistency for non-critical data (payer catalog, lookups)
- Document cache TTL in API documentation for transparency

**3. High Availability**:
- Always provision 2 dedicated gateway nodes (99.95% SLA)
- Configure SDK with fallback to direct endpoint
- Monitor gateway health in Azure Monitor
- Set up alerts for gateway unavailability or high error rates

**4. Performance Monitoring**:
- Track P95 latency before and after cache enablement
- Monitor cache hit rates per container
- Alert on cache hit rate <75%
- Regular (monthly) review of cache effectiveness

---

## Alternative Caching Strategies

### Comparison: Integrated Cache vs Alternatives

| Strategy | Cost (Year 3) | Complexity | Latency | Hit Rate | Recommendation |
|----------|---------------|------------|---------|----------|----------------|
| **Integrated Cache** | $150/month | Low | 1-2ms | 85-95% | ? **Recommended** |
| **Azure Cache for Redis** | $250-500/month | High | 1-3ms | 90-95% | ? Overkill for this scale |
| **Application-level cache** (IMemoryCache) | $0 | Medium | <1ms | 60-80% | ?? Only for stateless scenarios |
| **No cache** | $0 | Low | 8-12ms | N/A | ? Wastes RUs on repeat reads |

### Why Integrated Cache Wins

**Advantages**:
- ? No additional infrastructure to manage
- ? Automatic cache invalidation on writes
- ? Built-in high availability (2 nodes)
- ? No serialization overhead
- ? Transparent to application code
- ? Lower cost than Redis at this scale
- ? Better hit rate than app-level cache (shared across instances)
- ? No stale data concerns (TTL-based expiration)

**Disadvantages**:
- ?? Not cost-effective at very small scale (<100 practices)
- ?? Limited to Cosmos DB data only
- ?? 10GB cache size limit per node
- ?? Requires dedicated gateway nodes (adds latency vs direct connection)

### When to Consider Redis

Consider Azure Cache for Redis when:
- Year 5+ (1,000+ practices)
- Need for cross-service caching (beyond Cosmos)
- Complex cache invalidation logic required
- Session state management across multiple services
- Cache size requirements exceed 20GB
- Need for advanced cache features (pub/sub, sorted sets, etc.)

### When to Use Application-Level Cache

Consider IMemoryCache when:
- Stateless, single-instance scenarios
- Very small, frequently-accessed datasets (<100 items)
- Sub-millisecond latency required
- No cost budget for infrastructure
- Acceptable to lose cache on app restart

**Not Recommended** for BF.API because:
- ? Multi-instance deployment (cache not shared)
- ? Working set too large (20-32MB per practice)
- ? Cache invalidation complexity
- ? HIPAA compliance concerns (PHI in app memory)

---

## Summary & Final Recommendation

### Cost-Benefit Summary (Year 3 - 500 practices)

| Metric | Value |
|--------|-------|
| **Monthly Cosmos cost (without cache)** | $1,112 |
| **Monthly Cosmos cost (with cache)** | $848 |
| **Monthly cache cost** | $150 |
| **Total monthly cost (with cache)** | $998 |
| **Monthly savings** | **$114** |
| **Annual savings** | **$1,368** |
| **ROI** | **76%** (savings / cache cost) |
| **Latency improvement** | **75-80%** (P95) |
| **Cache hit rate (projected)** | **85-95%** |

### Phased Implementation Plan

```
Year 1 (50 practices):
? Do NOT implement cache
- Cost: $189/month (serverless, no cache)
- Cache would cost $250/month total (net loss of $61/month)
- Focus: Monitor access patterns, prepare for cache enablement

Year 2 (200 practices):
? IMPLEMENT cache when reaching 150-200 practices
- Trigger: Monthly Cosmos cost exceeds $500/month
- Cache config: 2 × D4s nodes (4GB each), $150/month
- Expected savings: $200-250/month
- Expected ROI: 130-170%

Year 3+ (500 practices):
? OPTIMIZE cache configuration
- Monitor hit rates, adjust TTL
- Scale to 8GB or 16GB nodes if eviction rate >10%
- Expected savings: $114+/month
- Expected ROI: 76%+
```

### Decision Criteria

**IMPLEMENT cache when:**
- ? Practice count ? 150-200
- ? Monthly Cosmos read costs ? $400/month
- ? Repeat access patterns confirmed (>60% cache hit potential)
- ? Budget approved for $150/month cache infrastructure

**DELAY cache when:**
- ? Practice count < 100
- ? Monthly Cosmos costs < $300/month
- ? Cache hit rate projections < 60%
- ? Budget constraints prohibit $150/month expense

### Final Recommendation

? **IMPLEMENT Azure Cosmos DB Integrated Cache** with the following conditions:

**Implementation Timing**:
- **Wait until Year 2** (200+ practices) when cost savings justify cache expense
- **Monitor trigger**: When monthly Cosmos read costs exceed $400/month
- **Review quarterly**: Assess ROI and adjust cache configuration

**Configuration**:
- 2 × dedicated gateway nodes (D4s v3, 4GB cache each)
- TTL: 2-5 minutes (patient data), 1 hour (reference data)
- Total cache budget: $150/month
- Region: Same as primary Cosmos DB region

**Expected Outcomes**:
- 50-80% reduction in read RU consumption
- 75-80% improvement in P95 latency
- 85-95% cache hit rate
- Net monthly savings: $114-250/month (depending on scale)
- Improved user experience during peak check-in hours
- Consistent performance across all practice sizes

**Success Criteria**:
- Cache hit rate >80% (target: 85-95%)
- Read RU/s reduction >60%
- P95 latency <3ms for cached operations
- Monthly cost savings >$100
- Zero cache-related availability incidents

**Monitoring Plan**:
- Daily: Check IntegratedCacheHitRate, EvictionRate
- Weekly: Review TotalRequestUnits, cost trends
- Monthly: Analyze ROI, adjust TTL and cache size
- Quarterly: Evaluate cost-benefit, consider scaling decisions
- Action: Disable if ROI falls below 50% for 2 consecutive months

**Fallback Plan**:
- Maintain direct endpoint connection as backup
- Implement automatic failover in SDK
- Monitor gateway health and latency
- Document cache removal procedure if ROI negative

---

## Related Documentation

- [Coverage Decision and Eligibility Checks](./CoverageDecision-and-Eligibility-Checks.md)
- [ForceRefresh Implementation](./ForceRefresh-Implementation.md)
- [Cosmos DB Modeling Session](./Cosmos-Modeling-Session.md) *(if exists)*
- [Azure Cost Management](./Azure-Cost-Management.md) *(if exists)*

---

**Document Version:** 1.0  
**Last Updated:** 2024-01-15  
**Author:** BF.API Development Team  
**Reviewed By:** Infrastructure Team

---

## Appendix: Technical Implementation Checklist

### Pre-Implementation (Year 1-2)

- [ ] Set up Application Insights tracking for cache hit potential
- [ ] Monitor practice count and Cosmos DB costs monthly
- [ ] Document access patterns and repeat read frequency
- [ ] Establish baseline latency metrics (P50, P95, P99)
- [ ] Calculate current RU/s consumption by operation type
- [ ] Get budget approval for $150/month cache infrastructure

### Implementation (Year 2)

- [ ] Provision 2 dedicated gateway nodes (D4s v3)
- [ ] Configure gateway endpoint in Azure Portal
- [ ] Update CosmosClient connection string to use gateway endpoint
- [ ] Implement TTL settings per container
- [ ] Add cache monitoring to Azure Monitor dashboards
- [ ] Set up alerts for cache hit rate <75%, eviction rate >10%
- [ ] Update SDK code to include DedicatedGatewayRequestOptions
- [ ] Deploy to staging environment for testing
- [ ] Load test to verify cache effectiveness
- [ ] Deploy to production with monitoring

### Post-Implementation Monitoring (Ongoing)

- [ ] Daily: Check cache hit rate and eviction rate
- [ ] Weekly: Review RU consumption and cost trends
- [ ] Monthly: Calculate ROI and savings
- [ ] Quarterly: Optimize TTL settings based on observed patterns
- [ ] Annually: Evaluate need for cache size increase or alternative strategies

### Optimization Triggers

- [ ] If cache hit rate <75%: Increase TTL or investigate access patterns
- [ ] If eviction rate >10%: Increase cache size to 8GB or 16GB nodes
- [ ] If ROI <50% for 2 months: Consider disabling cache
- [ ] If practice count >500: Consider Redis or CDN for static assets
- [ ] If P95 latency >5ms: Check network latency, gateway health

---

**End of Document**
