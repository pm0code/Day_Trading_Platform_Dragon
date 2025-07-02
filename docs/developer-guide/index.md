# Developer Guide

## Development Setup

### Prerequisites

- Git
- Node.js 18+ / .NET 8+ / Python 3.8+
- VS Code or your preferred IDE
- Docker (optional)

### Environment Setup

1. **Clone the Repository**
   ```bash
   git clone <repository-url>
   cd project-name
   ```

2. **Install Dependencies**
   ```bash
   npm install
   npm run prepare  # Set up git hooks
   ```

3. **Configure Environment**
   ```bash
   cp .env.example .env
   # Edit .env with your settings
   ```

## Project Structure

```
/
├── src/              # Source code
│   ├── core/         # Core functionality
│   ├── utils/        # Utilities
│   └── services/     # Service layer
├── tests/            # Test files
├── docs/             # Documentation
└── scripts/          # Build scripts
```

## Coding Standards

### Code Style

- Use ESLint/Prettier for JavaScript/TypeScript
- Follow C# coding conventions for .NET
- Use Black formatter for Python

### Naming Conventions

- **Classes**: PascalCase
- **Functions**: camelCase
- **Constants**: UPPER_SNAKE_CASE
- **Files**: kebab-case

## Testing

### Unit Tests

```bash
npm test           # Run all tests
npm test:watch     # Watch mode
npm test:coverage  # Coverage report
```

### Integration Tests

```bash
npm run test:integration
```

## Building

### Development Build

```bash
npm run build:dev
```

### Production Build

```bash
npm run build
```

## Debugging

### VS Code

1. Open the project in VS Code
2. Press F5 to start debugging
3. Set breakpoints as needed

### Chrome DevTools

1. Run with `--inspect` flag
2. Open chrome://inspect
3. Click "inspect"

## Contributing

### Workflow

1. Create feature branch
2. Make changes
3. Write tests
4. Submit PR

### Commit Messages

Follow conventional commits:
- `feat:` New features
- `fix:` Bug fixes
- `docs:` Documentation
- `test:` Tests
- `refactor:` Code refactoring

## API Development

### Adding New Endpoints

1. Define route in router
2. Implement controller
3. Add validation
4. Write tests
5. Update API docs

### Example

```typescript
// routes/example.ts
router.post('/example', validate(schema), controller.handleExample);

// controllers/example.ts
export async function handleExample(req: Request, res: Response) {
    const result = await service.process(req.body);
    res.json(result);
}
```

## Deployment

### Local Deployment

```bash
npm run start:prod
```

### Docker Deployment

```bash
docker build -t app .
docker run -p 3000:3000 app
```

## Troubleshooting

### Common Issues

1. **Module not found**: Run `npm install`
2. **Port in use**: Change PORT in .env
3. **Build fails**: Clear cache with `npm run clean`
