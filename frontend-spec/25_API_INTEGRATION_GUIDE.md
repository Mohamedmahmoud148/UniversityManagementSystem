# 25 — API Integration Guide

## Setup

### Axios Instances
```typescript
// lib/axios.ts
import axios from 'axios';

export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
  headers: { 'Content-Type': 'application/json' },
  timeout: 30000,
});

export const aiClient = axios.create({
  baseURL: import.meta.env.VITE_AI_SERVICE_URL,
  headers: { 'Content-Type': 'application/json' },
  timeout: 60000, // AI needs more time
});

// Attach auth token
apiClient.interceptors.request.use((config) => {
  const token = localStorage.getItem('access_token');
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

aiClient.interceptors.request.use((config) => {
  const token = localStorage.getItem('access_token');
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

// Auto-refresh on 401
apiClient.interceptors.response.use(
  (res) => res,
  async (error) => {
    const original = error.config;
    if (error.response?.status === 401 && !original._retry) {
      original._retry = true;
      const ok = await useAuthStore.getState().refreshTokens();
      if (ok) return apiClient(original);
    }
    return Promise.reject(error);
  }
);
```

---

## Multipart File Upload Pattern
```typescript
// For any endpoint accepting files
async function uploadFile(file: File, offeringId: string): Promise<MaterialDto> {
  const formData = new FormData();
  formData.append('file', file);
  formData.append('offeringId', offeringId);
  formData.append('title', file.name);
  
  return apiClient.post('/api/materials/upload', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
    onUploadProgress: (e) => {
      const progress = Math.round((e.loaded / (e.total || 1)) * 100);
      setUploadProgress(progress);
    },
  }).then(r => r.data);
}
```

---

## Paginated Query Pattern
```typescript
function useStudents(filters: StudentFilterDto) {
  return useQuery({
    queryKey: ['students', filters],
    queryFn: () => studentsApi.getFiltered(filters),
    placeholderData: keepPreviousData, // keeps old data during page change
  });
}
```

---

## Error Response Handling
```typescript
// types/errors.ts
interface ApiValidationError {
  status: 400;
  errors: Record<string, string[]>; // field → messages
}

interface ApiAuthError { status: 401 | 403; message: string; }
interface ApiNotFound { status: 404; message: string; }
interface ApiServerError { status: 500; message: string; }

function handleApiError(error: unknown): string {
  if (axios.isAxiosError(error)) {
    const status = error.response?.status;
    const data = error.response?.data;
    
    if (status === 400 && data?.errors) {
      return Object.values(data.errors).flat().join(', ');
    }
    if (status === 401) return 'Session expired. Please login again.';
    if (status === 403) return 'You do not have permission for this action.';
    if (status === 404) return data?.message || 'Item not found.';
    if (status >= 500) return 'Server error. Please try again later.';
    return data?.message || 'An error occurred.';
  }
  return 'Network error. Check your connection.';
}
```

---

## Arabic API Responses
Some API responses from the AI service are in Arabic. The frontend should display them as-is (they're already properly formatted). Do NOT translate AI-generated Arabic responses.

---

## Date Handling
All dates from API are **ISO 8601 UTC strings**. Display using:
```typescript
import { format, formatRelative, formatDistanceToNow } from 'date-fns';
import { ar } from 'date-fns/locale';

const locale = document.documentElement.lang === 'ar' ? ar : undefined;

// Absolute date
format(new Date(isoString), 'dd/MM/yyyy HH:mm', { locale });

// Relative
formatRelative(new Date(isoString), new Date(), { locale });
```

---

## Rate Limiting
The API has rate limiting. Handle 429 responses:
```typescript
if (error.response?.status === 429) {
  const retryAfter = error.response.headers['retry-after'] || 60;
  toast.error(`Rate limited. Try again in ${retryAfter}s`);
}
```
