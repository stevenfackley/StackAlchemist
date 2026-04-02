import { describe, it, expect } from 'vitest';

describe('SimpleMode', () => {
  it('should render the terminal textarea with placeholder text', () => {
    // render(<SimpleMode />);
    // expect(screen.getByRole('textbox')).toBeInTheDocument();
    expect(true).toBe(true); // Scaffold: implement when SimpleMode component exists
  });

  it('should show loading state on prompt submission', async () => {
    expect(true).toBe(true); // Scaffold
  });

  it('should transition to entity canvas when schema is received', async () => {
    expect(true).toBe(true); // Scaffold
  });

  it('should display error toast when API call fails', async () => {
    // server.use(http.post('/api/schema/extract', () => HttpResponse.json({error:'fail'}, {status:503})));
    expect(true).toBe(true); // Scaffold
  });

  it('should disable submit button when textarea is empty', () => {
    expect(true).toBe(true); // Scaffold
  });

  it('should submit on Ctrl+Enter keyboard shortcut', async () => {
    expect(true).toBe(true); // Scaffold
  });
});
