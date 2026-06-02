# 29 — Loading States

## Loading Strategies by Component

### Page Loading (Skeleton)
```tsx
function DashboardSkeleton() {
  return (
    <div className="space-y-6">
      {/* Stats row */}
      <div className="grid grid-cols-3 gap-4">
        {Array.from({ length: 3 }).map((_, i) => (
          <Skeleton key={i} className="h-32 rounded-xl" />
        ))}
      </div>
      {/* Two panels */}
      <div className="grid grid-cols-2 gap-4">
        <Skeleton className="h-64 rounded-xl" />
        <Skeleton className="h-64 rounded-xl" />
      </div>
    </div>
  );
}
```

### Table Loading
```tsx
function TableSkeleton({ rows = 5, cols = 5 }) {
  return (
    <div className="border rounded-lg overflow-hidden">
      {Array.from({ length: rows }).map((_, i) => (
        <div key={i} className="flex gap-4 p-4 border-b">
          {Array.from({ length: cols }).map((_, j) => (
            <Skeleton key={j} className="h-4 flex-1 rounded" />
          ))}
        </div>
      ))}
    </div>
  );
}
```

### Card Grid Loading
```tsx
function CardGridSkeleton({ count = 4 }) {
  return (
    <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
      {Array.from({ length: count }).map((_, i) => (
        <div key={i} className="border rounded-xl p-4 space-y-3">
          <Skeleton className="h-8 w-8 rounded-lg" />
          <Skeleton className="h-4 w-2/3" />
          <Skeleton className="h-8 w-1/2" />
          <Skeleton className="h-3 w-3/4" />
        </div>
      ))}
    </div>
  );
}
```

### Button Loading State
```tsx
<Button disabled={isPending}>
  {isPending ? (
    <><Loader2 className="w-4 h-4 mr-2 animate-spin" /> Processing...</>
  ) : 'Submit'}
</Button>
```

### AI Thinking Animation
```tsx
function AITypingIndicator() {
  return (
    <div className="flex items-center gap-2 px-4 py-3">
      <div className="w-8 h-8 rounded-full bg-gradient-to-br from-blue-500 to-purple-600 flex items-center justify-center text-white text-xs">AI</div>
      <div className="flex gap-1">
        {[0, 150, 300].map(delay => (
          <div 
            key={delay}
            className="w-2 h-2 bg-primary rounded-full animate-bounce"
            style={{ animationDelay: `${delay}ms` }}
          />
        ))}
      </div>
    </div>
  );
}
```

### File Upload Progress
```tsx
function UploadProgress({ progress }: { progress: number }) {
  return (
    <div className="space-y-2">
      <div className="flex justify-between text-sm">
        <span>Uploading...</span>
        <span>{progress}%</span>
      </div>
      <Progress value={progress} />
    </div>
  );
}
```

### Exam Generation Loading (AI — ~30 seconds)
```tsx
function ExamGeneratingLoader() {
  const steps = [
    'Analyzing subject requirements...',
    'Selecting relevant topics...',
    'Generating questions...',
    'Applying difficulty balance...',
    'Finalizing exam...',
  ];
  const [step, setStep] = useState(0);
  
  useEffect(() => {
    const interval = setInterval(() => {
      setStep(s => Math.min(s + 1, steps.length - 1));
    }, 5000);
    return () => clearInterval(interval);
  }, []);
  
  return (
    <div className="flex flex-col items-center gap-4 py-12">
      <Sparkles className="w-12 h-12 text-primary animate-pulse" />
      <h3 className="font-semibold text-lg">AI is creating your exam...</h3>
      <p className="text-muted-foreground">{steps[step]}</p>
      <Progress value={(step / steps.length) * 100} className="w-64" />
    </div>
  );
}
```

## Loading State Rules
1. ALWAYS show skeleton, never blank screen
2. Minimum 200ms delay before showing skeleton (prevents flash)
3. AI operations always show "AI is thinking..." with animation
4. File uploads always show progress bar
5. Long operations (>10s) show step-by-step status
