import React, { useEffect, useState } from 'react';
import { getLanguages } from '../services/apiClient';
import type { LanguageOption } from '../types/translation';

interface LanguageSelectorProps {
  selectedLanguage: string;
  onLanguageChange: (language: string) => void;
  disabled?: boolean;
}

const LanguageSelector: React.FC<LanguageSelectorProps> = ({
  selectedLanguage,
  onLanguageChange,
  disabled = false,
}) => {
  const [languages, setLanguages] = useState<LanguageOption[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchLanguages = async () => {
      try {
        const response = await getLanguages();
        setLanguages(response.languages);
        setError(null);
      } catch {
        setError('Failed to load languages');
        // Provide fallback languages
        setLanguages([
          { code: 'es', name: 'Spanish' },
          { code: 'fr', name: 'French' },
          { code: 'de', name: 'German' },
          { code: 'ja', name: 'Japanese' },
          { code: 'zh-Hans', name: 'Chinese (Simplified)' },
          { code: 'pt', name: 'Portuguese' },
          { code: 'it', name: 'Italian' },
          { code: 'ko', name: 'Korean' },
          { code: 'ar', name: 'Arabic' },
          { code: 'ru', name: 'Russian' },
        ]);
      } finally {
        setLoading(false);
      }
    };

    fetchLanguages();
  }, []);

  return (
    <div className="language-selector" style={{ marginTop: '16px' }}>
      <label htmlFor="target-language" style={{ display: 'block', marginBottom: '8px', fontWeight: 'bold' }}>
        Target Language
      </label>
      <select
        id="target-language"
        value={selectedLanguage}
        onChange={(e) => onLanguageChange(e.target.value)}
        disabled={disabled || loading}
        style={{
          padding: '8px 12px',
          borderRadius: '4px',
          border: '1px solid #ccc',
          fontSize: '1em',
          width: '100%',
          maxWidth: '300px',
        }}
      >
        <option value="">Select a language...</option>
        {languages.map((lang) => (
          <option key={lang.code} value={lang.code}>
            {lang.name}
          </option>
        ))}
      </select>
      {error && <p style={{ color: '#f57c00', fontSize: '0.85em', marginTop: '4px' }}>⚠️ Using default language list</p>}
    </div>
  );
};

export default LanguageSelector;
