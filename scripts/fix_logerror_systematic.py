#!/usr/bin/env python3
"""
Systematic fix for LogError parameter order issues.
Handles the pattern where LogError is called with placeholders and values as separate parameters.
"""

import os
import re
import sys
from pathlib import Path
from typing import List, Tuple

class LogErrorFixer:
    def __init__(self):
        self.fixed_count = 0
        self.files_processed = 0
        self.files_modified = 0
        
    def fix_logerror_patterns(self, content: str) -> Tuple[str, int]:
        """Fix LogError patterns in the content."""
        changes = 0
        lines = content.split('\n')
        
        for i, line in enumerate(lines):
            original_line = line
            
            # Pattern 1: LogError("Message {Placeholder}", value, ex)
            # Should become: LogError($"Message {value}", ex)
            pattern1 = r'((?:TradingLogOrchestrator\.Instance\.|_logger\.)LogError\()"([^"]+\{[^}]+\}[^"]*)"(\s*,\s*)([^,]+)(,\s*)(ex\b)'
            match1 = re.search(pattern1, line)
            if match1:
                prefix = match1.group(1)
                message = match1.group(2)
                value = match1.group(4).strip()
                
                # Extract placeholder names
                placeholders = re.findall(r'\{([^}]+)\}', message)
                if len(placeholders) == 1:
                    # Replace placeholder with actual value
                    new_message = message.replace(f'{{{placeholders[0]}}}', f'{{{value}}}')
                    line = f'{prefix}$"{new_message}", ex'
                    lines[i] = line
                    changes += 1
                    continue
            
            # Pattern 2: LogError("Message {Placeholder1} and {Placeholder2}", value1, value2, ex)
            pattern2 = r'((?:TradingLogOrchestrator\.Instance\.|_logger\.)LogError\()"([^"]+\{[^}]+\}[^"]*\{[^}]+\}[^"]*)"(\s*,\s*)([^,]+)(,\s*)([^,]+)(,\s*)(ex\b)'
            match2 = re.search(pattern2, line)
            if match2:
                prefix = match2.group(1)
                message = match2.group(2)
                value1 = match2.group(4).strip()
                value2 = match2.group(6).strip()
                
                # Extract placeholder names
                placeholders = re.findall(r'\{([^}]+)\}', message)
                if len(placeholders) == 2:
                    # Replace placeholders with actual values
                    new_message = message.replace(f'{{{placeholders[0]}}}', f'{{{value1}}}')
                    new_message = new_message.replace(f'{{{placeholders[1]}}}', f'{{{value2}}}')
                    line = f'{prefix}$"{new_message}", ex'
                    lines[i] = line
                    changes += 1
                    continue
            
            # Pattern 3: LogError(ex, "message") - wrong order entirely
            pattern3 = r'((?:TradingLogOrchestrator\.Instance\.|_logger\.)LogError\()(ex\s*,\s*)"([^"]+)"'
            match3 = re.search(pattern3, line)
            if match3:
                prefix = match3.group(1)
                message = match3.group(3)
                line = re.sub(pattern3, f'{prefix}"{message}", ex', line)
                lines[i] = line
                changes += 1
                continue
            
            # Pattern 4: LogError with named parameters but wrong order
            # e.g., LogError("Message", operationContext: "context", ex)
            pattern4 = r'((?:TradingLogOrchestrator\.Instance\.|_logger\.)LogError\()([^,]+)(,\s*\w+:\s*[^,]+)*(,\s*)(ex\b)([^)]*\))'
            match4 = re.search(pattern4, line)
            if match4 and not line.strip().startswith('//'):
                # Check if 'ex' appears before other named parameters
                if ', ex' in line and 'LogError(' in line:
                    # Extract the full method call
                    start_idx = line.find('LogError(')
                    paren_count = 0
                    end_idx = start_idx + 9  # Length of 'LogError('
                    
                    for j in range(end_idx, len(line)):
                        if line[j] == '(':
                            paren_count += 1
                        elif line[j] == ')':
                            if paren_count == 0:
                                end_idx = j + 1
                                break
                            paren_count -= 1
                    
                    method_call = line[start_idx:end_idx]
                    
                    # Check if this needs fixing (if message has placeholders with separate values)
                    if '{' in method_call and '}' in method_call and '", ' in method_call:
                        # This needs more complex handling - mark for manual review
                        print(f"Complex pattern in {i+1}: {line.strip()}")
        
        return '\n'.join(lines), changes
    
    def process_file(self, filepath: Path) -> bool:
        """Process a single file."""
        try:
            with open(filepath, 'r', encoding='utf-8') as f:
                content = f.read()
            
            fixed_content, changes = self.fix_logerror_patterns(content)
            
            if changes > 0:
                # Create backup
                backup_path = filepath.with_suffix(filepath.suffix + '.bak_logerror')
                with open(backup_path, 'w', encoding='utf-8') as f:
                    f.write(content)
                
                # Write fixed content
                with open(filepath, 'w', encoding='utf-8') as f:
                    f.write(fixed_content)
                
                print(f"✓ Fixed {changes} LogError calls in {filepath}")
                self.fixed_count += changes
                self.files_modified += 1
                return True
            else:
                print(f"  No changes needed in {filepath}")
                return False
                
        except Exception as e:
            print(f"✗ Error processing {filepath}: {e}")
            return False
    
    def find_files_with_issues(self, root_dir: Path) -> List[Path]:
        """Find all C# files with potential LogError issues."""
        files = []
        for filepath in root_dir.rglob("*.cs"):
            try:
                with open(filepath, 'r', encoding='utf-8') as f:
                    content = f.read()
                    # Look for LogError calls with placeholders
                    if re.search(r'LogError\([^)]*\{[^}]+\}[^)]*,', content):
                        files.append(filepath)
            except Exception as e:
                print(f"Error reading {filepath}: {e}")
        return files
    
    def run(self, root_dir: str):
        """Run the fixer on all files in the directory."""
        root_path = Path(root_dir)
        print(f"Scanning for files with LogError issues in {root_path}...")
        
        files = self.find_files_with_issues(root_path)
        print(f"Found {len(files)} files with potential issues")
        
        for i, filepath in enumerate(files, 1):
            print(f"\n[{i}/{len(files)}] Processing {filepath.relative_to(root_path)}...")
            self.process_file(filepath)
            self.files_processed += 1
        
        print(f"\n{'='*60}")
        print(f"Summary:")
        print(f"  Files processed: {self.files_processed}")
        print(f"  Files modified: {self.files_modified}")
        print(f"  Total fixes: {self.fixed_count}")
        print(f"{'='*60}")

if __name__ == "__main__":
    if len(sys.argv) > 1:
        root_dir = sys.argv[1]
    else:
        root_dir = "."
    
    fixer = LogErrorFixer()
    fixer.run(root_dir)