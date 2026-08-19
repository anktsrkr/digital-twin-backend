import { useState, useEffect, useMemo } from 'react';
import { CopilotKit } from '@copilotkit/react-core';
import { HeroHeader } from './components/HeroHeader';
import { DigitalTwinChat } from './components/DigitalTwinChat';
import { AuthModal } from './components/AuthModal';
import { CitationDrawer, type CitationDetail } from './components/CitationDrawer';
import { supabase } from './lib/supabaseClient';
import './styles/index.css';

export function App() {
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [recruiterEmail, setRecruiterEmail] = useState<string | undefined>(undefined);
  const [recruiterCompany, setRecruiterCompany] = useState<string | undefined>(undefined);
  const [isAuthOpen, setIsAuthOpen] = useState(false);
  const [selectedCitation, setSelectedCitation] = useState<CitationDetail | null>(null);

  const backendUrl = typeof window !== 'undefined' && window.location.hostname === 'localhost'
    ? 'http://localhost:5000'
    : (import.meta.env.VITE_BACKEND_API_URL || import.meta.env.VITE_API_URL || 'http://localhost:5000');

  useEffect(() => {
    // Check saved session in localStorage
    const savedEmail = localStorage.getItem('recruiter_email');
    const savedCompany = localStorage.getItem('recruiter_company');
    if (savedEmail) {
      setIsAuthenticated(true);
      setRecruiterEmail(savedEmail);
      if (savedCompany) setRecruiterCompany(savedCompany);
    }

    // Listen to Supabase Auth State Changes
    const { data: { subscription } } = supabase.auth.onAuthStateChange((_event, session) => {
      if (session?.user?.email) {
        setIsAuthenticated(true);
        setRecruiterEmail(session.user.email);
        localStorage.setItem('recruiter_email', session.user.email);
      }
    });

    return () => {
      subscription.unsubscribe();
    };
  }, []);

  const handleAuthSuccess = (email: string, company?: string) => {
    setIsAuthenticated(true);
    setRecruiterEmail(email);
    if (company) setRecruiterCompany(company);
    localStorage.setItem('recruiter_email', email);
    if (company) localStorage.setItem('recruiter_company', company);
  };

  const handleSignOut = async () => {
    try {
      await supabase.auth.signOut();
    } catch { }
    setIsAuthenticated(false);
    setRecruiterEmail(undefined);
    setRecruiterCompany(undefined);
    localStorage.removeItem('recruiter_email');
    localStorage.removeItem('recruiter_company');
  };

  const copilotHeaders = useMemo(() => ({
    ...(recruiterEmail ? { 'X-Recruiter-Email': recruiterEmail } : {}),
    ...(recruiterCompany ? { 'X-Recruiter-Company': recruiterCompany } : {})
  }), [recruiterEmail, recruiterCompany]);

  return (
    <CopilotKit
      runtimeUrl={`${backendUrl}/agentic_chat`}
      agent="agentic_chat"
      showDevConsole={false}
      headers={copilotHeaders}
    >
      <div style={{ minHeight: '100vh', display: 'flex', flexDirection: 'column' }}>
        <HeroHeader
          isAuthenticated={isAuthenticated}
          recruiterEmail={recruiterEmail}
          recruiterCompany={recruiterCompany}
          onOpenAuth={() => setIsAuthOpen(true)}
          onSignOut={handleSignOut}
          onScheduleClick={() => window.open('https://cal.com/anktsrkr', '_blank')}
          onDownloadPdf={() => {
            const link = document.createElement('a');
            link.href = '/resume.pdf';
            link.download = 'Ankit_Sarkar_AI_Solutions_Architect_Resume.pdf';
            link.click();
          }}
        />

        <main style={{ flex: 1, padding: '0 1rem 1.5rem', display: 'flex', flexDirection: 'column' }}>
          <DigitalTwinChat
            isAuthenticated={isAuthenticated}
            recruiterEmail={recruiterEmail}
            onOpenAuth={() => setIsAuthOpen(true)}
            onOpenCitation={(citation) => setSelectedCitation(citation)}
          />
        </main>

        {/* Recruiter Magic Link Modal */}
        <AuthModal
          isOpen={isAuthOpen}
          onClose={() => setIsAuthOpen(false)}
          onSuccess={handleAuthSuccess}
        />

        {/* Interactive Citation Drawer */}
        <CitationDrawer
          isOpen={selectedCitation !== null}
          citation={selectedCitation}
          onClose={() => setSelectedCitation(null)}
        />
      </div>
    </CopilotKit>
  );
}

export default App;
