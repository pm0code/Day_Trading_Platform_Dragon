# Basic Example - Hello World

## Overview
This example demonstrates the most basic usage of the project.

## Code

```javascript
// Import the library
const { analyze } = require('project-name');

// Basic usage
async function main() {
    const result = await analyze({
        input: 'Hello, World!',
        options: {
            verbose: true
        }
    });
    
    console.log('Result:', result);
}

main().catch(console.error);
```

## Explanation

1. **Import**: Load the required modules
2. **Configure**: Set up options
3. **Execute**: Run the main function
4. **Handle**: Process results and errors

## Output

```
Result: {
    status: 'success',
    data: 'Analysis complete',
    metrics: {
        time: 0.123,
        operations: 5
    }
}
```

## Variations

### With Error Handling

```javascript
try {
    const result = await analyze(input);
    // Process result
} catch (error) {
    console.error('Analysis failed:', error);
}
```

### With Custom Options

```javascript
const options = {
    verbose: true,
    format: 'json',
    timeout: 5000
};

const result = await analyze(input, options);
```
