# 26 — React Query Strategy

## Configuration
```typescript
// lib/queryClient.ts
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 5 * 60 * 1000,     // 5 min default
      gcTime: 10 * 60 * 1000,       // 10 min cache
      retry: 2,
      retryDelay: attempt => Math.min(1000 * 2 ** attempt, 30000),
      refetchOnWindowFocus: false,
    },
  },
});
```

## Stale Time by Data Category

| Data Type | staleTime | Notes |
|-----------|-----------|-------|
| Auth/Me | 10 min | Changes rarely |
| Dashboard | 5 min | Check often |
| Grades/GPA | 10 min | Rarely change mid-session |
| Notifications | 1 min + polling | Need freshness |
| Active exam | 1 min | Keep fresh during exam |
| Exams list | 5 min | |
| Teaching Intelligence | 10 min | Hourly backend refresh |
| Companion Dashboard | 5 min | |
| Flashcards due | 5 min | |
| Materials | 10 min | Rarely change |
| Regulations/Roadmap | 30 min | Very stable |

## Polling Queries
```typescript
// Notifications — poll every 60 seconds
refetchInterval: 60_000

// Dashboard — no polling (manual refresh button for teaching intel)

// Active exam session — poll every 30s for time sync
refetchInterval: 30_000
```

## Prefetching
Prefetch on hover for better perceived performance:
```typescript
function OfferingCard({ offering }) {
  const handleMouseEnter = () => {
    queryClient.prefetchQuery({
      queryKey: ['teaching', 'offerings', offering.offeringId, 'analytics'],
      queryFn: () => teachingApi.getClassIntelligence(offering.offeringId),
    });
  };
  return <Card onMouseEnter={handleMouseEnter}>{...}</Card>;
}
```

## Infinite Queries (Messages)
```typescript
function useChatMessages(conversationId: string) {
  return useInfiniteQuery({
    queryKey: ['chat', conversationId, 'messages'],
    queryFn: ({ pageParam = 1 }) => chatApi.getMessages(conversationId, pageParam),
    getNextPageParam: (lastPage, pages) => 
      lastPage.data.length === 50 ? pages.length + 1 : undefined,
    initialPageParam: 1,
  });
}
```
