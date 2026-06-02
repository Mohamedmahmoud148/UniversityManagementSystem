# 21 — Mobile Experience

## Bottom Navigation (Mobile)

### Student Mobile Nav
```
[🏠 Home] [📚 AI] [💬 Chat] [📝 Exams] [👤 Me]
```

### Doctor Mobile Nav
```
[📊 Dashboard] [👥 Students] [📝 Exams] [💬 Chat] [👤 Me]
```

## Touch Interactions

| Gesture | Action |
|---------|--------|
| Swipe right on flashcard | Easy |
| Swipe left on flashcard | Hard |
| Pull to refresh | Reload data |
| Long press notification | Mark read / Delete |
| Swipe left on list item | Reveal actions |
| Pinch to zoom on charts | Zoom in |

## QR Code Scanner (Attendance)
```tsx
// Student scans QR code from doctor's screen
import { Html5QrcodeScanner } from 'html5-qrcode';

function QRScanner({ onScan }) {
  useEffect(() => {
    const scanner = new Html5QrcodeScanner('qr-reader', { fps: 10, qrbox: 250 });
    scanner.render(
      (decodedText) => {
        // decodedText = sessionId from QR
        onScan(decodedText);
        scanner.clear();
      },
      (error) => console.warn(error)
    );
    return () => scanner.clear();
  }, []);
  
  return <div id="qr-reader" className="w-full" />;
}
```

## Mobile-First Key Pages

### Exam Taking (Mobile)
- Single question at a time (no sidebar navigator on mobile)
- Bottom sheet for question list
- Timer at top, fixed
- Next/Previous buttons at bottom

### Flashcard Review (Mobile)
- Full-screen card
- Tap anywhere to flip
- Swipe left/right to rate
- Visual feedback: green shimmer (easy), red shimmer (hard)

### AI Chat (Mobile)
- Full-screen conversation
- Suggestions as horizontal scroll chips
- Keyboard-aware (chat moves up when keyboard opens)

## Responsive Images
```tsx
// Student profile, material thumbnails
<img 
  src={imageUrl} 
  loading="lazy"
  className="w-full object-cover"
  alt={title}
/>
```

## PWA Support (Optional Enhancement)
```json
// manifest.json
{
  "name": "University AI Platform",
  "short_name": "UniAI",
  "theme_color": "#2563EB",
  "background_color": "#ffffff",
  "display": "standalone",
  "start_url": "/",
  "icons": [...]
}
```
