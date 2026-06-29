import { PlaygroundPage } from '@/features/playground/PlaygroundPage';

// Until routing lands (Step 2.1), the app renders the component playground so the primitives can be
// reviewed in light + dark. This is replaced by the real router + screens in the next phase.
export default function App() {
  return <PlaygroundPage />;
}
