import { http, HttpResponse } from 'msw';

const API_BASE = process.env.NEXT_PUBLIC_ENGINE_URL || 'http://localhost:5000';

export const schemaExtractionHandlers = [
  http.post(`${API_BASE}/api/schema/extract`, async () => {
    return HttpResponse.json({
      entities: [
        { id: 'entity-1', name: 'Product', fields: [
          { name: 'Id', type: 'uuid', isPrimaryKey: true },
          { name: 'Name', type: 'string', maxLength: 255, isRequired: true },
          { name: 'Price', type: 'decimal', isRequired: true },
          { name: 'CreatedAt', type: 'timestamp', isRequired: true },
        ]},
        { id: 'entity-2', name: 'Category', fields: [
          { name: 'Id', type: 'uuid', isPrimaryKey: true },
          { name: 'Name', type: 'string', maxLength: 100, isRequired: true },
        ]},
        { id: 'entity-3', name: 'Order', fields: [
          { name: 'Id', type: 'uuid', isPrimaryKey: true },
          { name: 'ProductId', type: 'uuid', isRequired: true },
          { name: 'Quantity', type: 'integer', isRequired: true },
          { name: 'TotalPrice', type: 'decimal', isRequired: true },
        ]},
      ],
      relationships: [
        { from: 'Product', to: 'Category', type: 'many-to-one', foreignKey: 'CategoryId' },
        { from: 'Order', to: 'Product', type: 'many-to-one', foreignKey: 'ProductId' },
      ],
    });
  }),
];

export const generationHandlers = [
  http.post(`${API_BASE}/api/generation/start`, async () => {
    return HttpResponse.json({ generationId: 'gen-test-uuid-1234', status: 'pending' });
  }),
  http.get(`${API_BASE}/api/generation/:id/status`, ({ params }) => {
    return HttpResponse.json({
      id: params.id, status: 'success', retryCount: 0,
      downloadUrl: 'https://r2.stackalchemist.app/test-download-url',
      createdAt: '2026-01-15T10:00:00Z', completedAt: '2026-01-15T10:02:30Z',
    });
  }),
];

export const checkoutHandlers = [
  http.post(`${API_BASE}/api/checkout/create-session`, async ({ request }) => {
    const body = (await request.json()) as { tier: number };
    return HttpResponse.json({ sessionId: 'cs_test_12345', url: `https://checkout.stripe.com/test/${body.tier}` });
  }),
];

export const dashboardHandlers = [
  http.get(`${API_BASE}/api/generations`, () => {
    return HttpResponse.json({ generations: [
      { id: 'gen-1', status: 'success', tier: 2, createdAt: '2026-01-15T10:00:00Z', downloadUrl: 'https://r2.stackalchemist.app/gen-1.zip' },
      { id: 'gen-2', status: 'failed', tier: 3, createdAt: '2026-01-14T15:30:00Z', downloadUrl: null },
      { id: 'gen-3', status: 'generating', tier: 2, createdAt: '2026-01-16T09:00:00Z', downloadUrl: null },
    ]});
  }),
];

export const byokHandlers = [
  http.get(`${API_BASE}/api/profile/settings`, () => {
    return HttpResponse.json({ email: 'test@example.com', hasApiKeyOverride: false, preferredModel: 'claude-3-5-sonnet' });
  }),
  http.put(`${API_BASE}/api/profile/api-key`, async () => {
    return HttpResponse.json({ success: true, message: 'API key updated and encrypted successfully.' });
  }),
];

export const handlers = [
  ...schemaExtractionHandlers, ...generationHandlers,
  ...checkoutHandlers, ...dashboardHandlers, ...byokHandlers,
];
