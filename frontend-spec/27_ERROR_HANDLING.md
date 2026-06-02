# 27 — Error Handling

## Error Hierarchy

```
Network Error (no connection)
  └── 401 Unauthorized → refresh token → if fail → logout
  └── 403 Forbidden → show "no permission" toast
  └── 404 Not Found → show empty state or navigate to 404
  └── 400 Validation → show field errors inline
  └── 429 Rate Limited → show countdown toast
  └── 500+ Server Error → show "try again" toast
```

## Toast Notifications (Errors)
Use Sonner or Shadcn toast:
```typescript
import { toast } from 'sonner';

// Error
toast.error('Failed to save', { description: errorMessage });

// Success  
toast.success('Exam submitted!');

// Warning
toast.warning('Assignment due in 2 hours');

// Info
toast.info('Data refreshed');
```

## Form Validation Errors
```tsx
// React Hook Form + Zod
const { register, formState: { errors } } = useForm({ resolver: zodResolver(schema) });

<Input {...register('email')} />
{errors.email && <p className="text-red-500 text-xs mt-1">{errors.email.message}</p>}
```

## API Error Display
```tsx
function ErrorAlert({ error }: { error: unknown }) {
  const message = handleApiError(error);
  return (
    <Alert variant="destructive">
      <AlertTriangle className="h-4 w-4" />
      <AlertTitle>Error</AlertTitle>
      <AlertDescription>{message}</AlertDescription>
    </Alert>
  );
}
```

## Error Boundaries
Wrap each major section:
```tsx
<ErrorBoundary fallback={<SectionError />}>
  <TeachingDashboard />
</ErrorBoundary>
```

## Special Error Cases

### Exam Submit Failure
If exam submit fails:
1. Show error toast
2. DO NOT clear answers
3. Keep exam state in Zustand
4. Offer retry button
5. If 401: refresh token then retry

### File Upload Failure
1. Show inline error under upload zone
2. Keep file selected for retry
3. Show error message from server

### AI Service Unavailable
1. Check `error.response?.status === 503`
2. Show banner: "AI service temporarily unavailable. Basic features still work."
3. Disable AI-specific buttons gracefully
