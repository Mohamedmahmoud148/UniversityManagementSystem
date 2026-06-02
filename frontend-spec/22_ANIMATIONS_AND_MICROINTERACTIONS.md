# 22 — Animations and Microinteractions

## Tool: Framer Motion

```tsx
import { motion, AnimatePresence } from 'framer-motion';
```

---

## Page Transitions

```tsx
const pageVariants = {
  initial: { opacity: 0, y: 20 },
  animate: { opacity: 1, y: 0, transition: { duration: 0.3 } },
  exit: { opacity: 0, y: -20, transition: { duration: 0.2 } },
};

function PageWrapper({ children }) {
  return (
    <motion.div variants={pageVariants} initial="initial" animate="animate" exit="exit">
      {children}
    </motion.div>
  );
}
```

---

## Card Animations

### Dashboard Stat Cards (staggered)
```tsx
const container = {
  animate: { transition: { staggerChildren: 0.1 } }
};
const item = {
  initial: { opacity: 0, y: 20 },
  animate: { opacity: 1, y: 0 }
};

<motion.div variants={container} initial="initial" animate="animate" className="grid grid-cols-3 gap-4">
  {stats.map(stat => (
    <motion.div key={stat.key} variants={item}>
      <MetricCard {...stat} />
    </motion.div>
  ))}
</motion.div>
```

### Risk Card (pulse on critical)
```tsx
{riskLevel === 'Critical' && (
  <motion.div
    animate={{ scale: [1, 1.02, 1] }}
    transition={{ repeat: Infinity, duration: 2 }}
  />
)}
```

---

## Flashcard Flip Animation

```tsx
function FlashcardFlip({ front, back, isFlipped, onFlip }) {
  return (
    <div className="perspective-1000">
      <motion.div
        animate={{ rotateY: isFlipped ? 180 : 0 }}
        transition={{ duration: 0.5, type: 'spring' }}
        className="relative w-full h-64"
        style={{ transformStyle: 'preserve-3d' }}
        onClick={onFlip}
      >
        <div className="absolute inset-0 backface-hidden flex items-center justify-center bg-card border rounded-2xl p-6">
          <p className="text-xl font-medium text-center">{front}</p>
        </div>
        <div className="absolute inset-0 backface-hidden flex items-center justify-center bg-primary text-primary-foreground border rounded-2xl p-6" style={{ transform: 'rotateY(180deg)' }}>
          <p className="text-xl text-center">{back}</p>
        </div>
      </motion.div>
    </div>
  );
}
```

### Card Rating Swipe Animation
```tsx
// After rating: slide card out
const [direction, setDirection] = useState<'left' | 'right' | null>(null);

<AnimatePresence>
  <motion.div
    key={card.id}
    animate={{ x: direction === 'right' ? 300 : direction === 'left' ? -300 : 0, opacity: direction ? 0 : 1 }}
    transition={{ duration: 0.3 }}
    onAnimationComplete={() => nextCard()}
  />
</AnimatePresence>
```

---

## Chart Animations (Recharts)

```tsx
// Line chart: draw animation
<Line isAnimationActive={true} animationDuration={800} animationEasing="ease-out" />

// Bar chart: rise from bottom
<Bar isAnimationActive={true} animationDuration={600} animationBegin={0} />
```

---

## Notification Bell Animation

```tsx
// Shake when new notification arrives
const { unreadCount } = useNotificationStore();
const [shake, setShake] = useState(false);

useEffect(() => {
  if (unreadCount > 0) {
    setShake(true);
    setTimeout(() => setShake(false), 500);
  }
}, [unreadCount]);

<motion.div
  animate={shake ? { rotate: [0, -10, 10, -10, 10, 0] } : {}}
  transition={{ duration: 0.5 }}
>
  <Bell />
</motion.div>
```

---

## Streak Milestone Celebration

```tsx
// When streak milestone is hit (7, 14, 30 days)
function StreakCelebration({ days }) {
  return (
    <motion.div
      initial={{ scale: 0 }}
      animate={{ scale: [0, 1.3, 1] }}
      transition={{ type: 'spring', stiffness: 500 }}
      className="fixed inset-0 flex items-center justify-center z-50"
    >
      <div className="bg-card rounded-3xl p-8 shadow-2xl text-center">
        <motion.div
          animate={{ rotate: [0, -20, 20, 0] }}
          transition={{ repeat: 3, duration: 0.3 }}
          className="text-6xl mb-4"
        >🔥</motion.div>
        <h2 className="text-2xl font-bold">{days} Day Streak!</h2>
      </div>
    </motion.div>
  );
}
```

---

## AI Response Animation

Typewriter effect for AI responses:
```tsx
function TypewriterText({ text, speed = 20 }) {
  const [displayed, setDisplayed] = useState('');
  
  useEffect(() => {
    let i = 0;
    const interval = setInterval(() => {
      if (i < text.length) {
        setDisplayed(text.slice(0, ++i));
      } else {
        clearInterval(interval);
      }
    }, speed);
    return () => clearInterval(interval);
  }, [text]);
  
  return <span>{displayed}</span>;
}
```

---

## Hover States

```css
/* Card hover */
.card { transition: transform 0.15s, box-shadow 0.15s; }
.card:hover { transform: translateY(-2px); box-shadow: 0 8px 25px rgba(0,0,0,0.1); }

/* Button hover */
.btn { transition: all 0.15s; }
.btn:hover { filter: brightness(0.92); }

/* Table row hover */
tr { transition: background 0.1s; }
tr:hover { background: rgba(0,0,0,0.02); }
```

---

## Exam Timer Warning Animation

```tsx
// Timer turns red + pulses under 10 minutes
{timeLeft < 600 && (
  <motion.div
    animate={{ opacity: [1, 0.5, 1] }}
    transition={{ repeat: Infinity, duration: 1 }}
    className="text-red-600 font-bold text-2xl"
  >
    ⏱ {formatTime(timeLeft)}
  </motion.div>
)}
```
