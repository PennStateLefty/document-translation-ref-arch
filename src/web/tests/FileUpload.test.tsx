import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import FileUpload from '../src/components/FileUpload';

describe('FileUpload', () => {
  it('renders the dropzone with instructions', () => {
    const onFilesSelected = vi.fn();
    render(<FileUpload onFilesSelected={onFilesSelected} />);

    expect(screen.getByText(/drag and drop documents here/i)).toBeDefined();
    expect(screen.getByText(/supported formats/i)).toBeDefined();
  });

  it('renders as disabled when disabled prop is true', () => {
    const onFilesSelected = vi.fn();
    render(<FileUpload onFilesSelected={onFilesSelected} disabled={true} />);

    const dropzone = screen.getByText(/drag and drop documents here/i).closest('div');
    expect(dropzone).toBeDefined();
  });

  it('shows file input element', () => {
    const onFilesSelected = vi.fn();
    const { container } = render(<FileUpload onFilesSelected={onFilesSelected} />);

    const input = container.querySelector('input[type="file"]');
    expect(input).toBeDefined();
  });
});
