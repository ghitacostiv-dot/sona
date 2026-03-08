import { useEffect, useState } from "react";
import { BrowserRouter as Router, Routes, Route } from "react-router-dom";
import { Sidebar } from "./components/Sidebar";
import { Home } from "./pages/Home";
import { Detail } from "./pages/Detail";
import { Addons } from "./pages/Addons";
import { Search } from "./pages/Search";
import { Library } from "./pages/Library";
import { Movies } from "./pages/Movies";
import { Series } from "./pages/Series";
import { LiveTV } from "./pages/LiveTV";
import { Anime } from "./pages/Anime";
import { Settings } from "./pages/Settings";
import { RadioPage } from "./pages/Radio";
import { Audiobooks } from "./pages/Audiobooks";
import { Courses } from "./pages/Courses";
import { AuthProvider } from "./context/AuthContext";
import { ErrorBoundary } from "./components/ErrorBoundary";
import { addonService } from "./services/addonService";

function AppInner() {
  const [ready, setReady] = useState(false);

  useEffect(() => {
    const theme = localStorage.getItem("nexus_theme") || "transparent";
    document.documentElement.setAttribute("data-theme", theme);
    addonService.initialize().finally(() => setReady(true));
  }, []);

  const hasCachedAddons = addonService.getAddons().length > 0;

  if (!hasCachedAddons && !ready) {
    return (
      <div className="flex items-center justify-center min-h-screen flex-col gap-4">
        <div className="w-12 h-12 border-4 border-brand border-t-transparent rounded-full animate-spin" />
        <p className="text-white/40 text-sm font-mono uppercase tracking-widest">Loading Nexus…</p>
      </div>
    );
  }

  return (
    <div className="flex min-h-screen bg-bg">
      <Sidebar />
      <main className="flex-1 px-8 py-8 overflow-y-auto min-w-0">
        <ErrorBoundary>
          <Routes>
            <Route path="/" element={<Home />} />
            <Route path="/detail/:type/:id" element={<Detail />} />
            <Route path="/addons" element={<Addons />} />
            <Route path="/search" element={<Search />} />
            <Route path="/library" element={<Library />} />
            <Route path="/movies" element={<Movies />} />
            <Route path="/series" element={<Series />} />
            <Route path="/anime" element={<Anime />} />
            <Route path="/tv" element={<LiveTV />} />
            <Route path="/radio" element={<RadioPage />} />
            <Route path="/audiobooks" element={<Audiobooks />} />
            <Route path="/courses" element={<Courses />} />
            <Route path="/settings" element={<Settings />} />
          </Routes>
        </ErrorBoundary>
      </main>
    </div>
  );
}

export default function App() {
  return (
    <AuthProvider>
      <Router>
        <ErrorBoundary>
          <AppInner />
        </ErrorBoundary>
      </Router>
    </AuthProvider>
  );
}
