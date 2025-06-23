#!/usr/bin/env python3
"""
Phase 2: Systematic fix for LogError, LogInfo, LogWarning parameter issues.
Converts template-style logging calls to proper string interpolation.
"""

import os
import re
import sys
from pathlib import Path
from typing import List, Tuple, Optional

class LoggingParameterFixer:
    def __init__(self):
        self.fixed_count = 0
        self.files_processed = 0
        self.files_modified = 0
        self.patterns_found = {}
        
    def extract_placeholder_values(self, line: str, start_pos: int) -> Tuple[str, List[str], int]:
        """Extract the full method call and parameter values."""
        paren_count = 1
        pos = start_pos
        method_call = ""
        in_string = False
        escape_next = False
        
        while pos < len(line) and paren_count > 0:
            char = line[pos]
            method_call += char
            
            if escape_next:
                escape_next = False
            elif char == '\\':
                escape_next = True
            elif char == '"' and not in_string:
                in_string = True
            elif char == '"' and in_string:
                in_string = False
            elif char == '(' and not in_string:
                paren_count += 1
            elif char == ')' and not in_string:
                paren_count -= 1
                
            pos += 1
            
        return method_call, [], pos
    
    def fix_logging_call(self, line: str, method_name: str) -> Tuple[str, bool]:
        """Fix a logging method call with placeholders."""
        # Pattern to match logging calls with placeholders
        pattern = rf'((?:TradingLogOrchestrator\.Instance\.|_logger\.){method_name}\()"([^"]+)"(.*?)(\);?)'
        
        match = re.search(pattern, line)
        if not match:
            return line, False
            
        prefix = match.group(1)
        message = match.group(2)
        params_section = match.group(3)
        suffix = match.group(4)
        
        # Check if message has placeholders
        placeholders = re.findall(r'\{([^}]+)\}', message)
        if not placeholders:
            return line, False
            
        # Parse parameters
        params = []
        param_str = params_section.strip()
        if param_str.startswith(','):
            param_str = param_str[1:].strip()
            
        # Simple parameter extraction (handles common cases)
        # This is a simplified approach - for complex cases, we'd need a full parser
        if param_str:
            # Split by commas not inside parentheses or quotes
            current_param = ""
            paren_depth = 0
            in_quotes = False
            
            for char in param_str:
                if char == '"' and (not current_param or current_param[-1] != '\\'):
                    in_quotes = not in_quotes
                elif char == '(' and not in_quotes:
                    paren_depth += 1
                elif char == ')' and not in_quotes:
                    paren_depth -= 1
                elif char == ',' and paren_depth == 0 and not in_quotes:
                    if current_param.strip():
                        params.append(current_param.strip())
                    current_param = ""
                    continue
                    
                current_param += char
                
            if current_param.strip():
                params.append(current_param.strip())
        
        # Build the fixed line
        if len(placeholders) <= len(params):
            # Replace placeholders with actual values using string interpolation
            new_message = message
            for i, placeholder in enumerate(placeholders):
                if i < len(params):
                    value = params[i]
                    new_message = new_message.replace(f'{{{placeholder}}}', f'{{{value}}}')
            
            # Reconstruct the call
            remaining_params = params[len(placeholders):]
            
            # Check if there's an exception parameter
            has_exception = any('ex' in p or 'exception' in p.lower() for p in remaining_params)
            
            if remaining_params:
                new_line = f'{prefix}$"{new_message}", {", ".join(remaining_params)}{suffix}'
            else:
                new_line = f'{prefix}$"{new_message}"{suffix}'
                
            return new_line, True
            
        return line, False
    
    def fix_line(self, line: str) -> Tuple[str, int]:
        """Fix all logging calls in a line."""
        changes = 0
        original_line = line
        
        # Fix different logging methods
        for method in ['LogError', 'LogInfo', 'LogWarning', 'LogDebug', 'LogTrace']:
            new_line, changed = self.fix_logging_call(line, method)
            if changed:
                line = new_line
                changes += 1
                self.patterns_found[method] = self.patterns_found.get(method, 0) + 1
                
        return line, changes
    
    def process_file(self, filepath: Path) -> bool:
        """Process a single file."""
        try:
            with open(filepath, 'r', encoding='utf-8') as f:
                lines = f.readlines()
            
            modified = False
            new_lines = []
            file_changes = 0
            
            for line_num, line in enumerate(lines, 1):
                new_line, changes = self.fix_line(line.rstrip('\n'))
                if changes > 0:
                    print(f"  Line {line_num}: Fixed {changes} logging call(s)")
                    modified = True
                    file_changes += changes
                    self.fixed_count += changes
                new_lines.append(new_line)
            
            if modified:
                # Create backup
                backup_path = filepath.with_suffix(filepath.suffix + '.bak_phase2')
                with open(backup_path, 'w', encoding='utf-8') as f:
                    f.writelines(line + '\n' for line in lines)
                
                # Write fixed content
                with open(filepath, 'w', encoding='utf-8') as f:
                    f.writelines(line + '\n' for line in new_lines)
                
                print(f"✓ Fixed {file_changes} logging calls in {filepath}")
                self.files_modified += 1
                return True
            else:
                return False
                
        except Exception as e:
            print(f"✗ Error processing {filepath}: {e}")
            return False
    
    def find_files_with_issues(self, root_dir: Path) -> List[Path]:
        """Find all C# files with potential logging issues."""
        files = []
        patterns = [
            r'Log(?:Error|Info|Warning|Debug|Trace)\([^)]*\{[^}]+\}[^)]*,',
            r'TradingLogOrchestrator\.Instance\.Log\w+\([^)]*\{[^}]+\}',
            r'_logger\.Log\w+\([^)]*\{[^}]+\}'
        ]
        
        for filepath in root_dir.rglob("*.cs"):
            if any(part.startswith('.') for part in filepath.parts):
                continue  # Skip hidden directories
                
            try:
                with open(filepath, 'r', encoding='utf-8') as f:
                    content = f.read()
                    
                for pattern in patterns:
                    if re.search(pattern, content):
                        files.append(filepath)
                        break
                        
            except Exception as e:
                print(f"Error reading {filepath}: {e}")
                
        return sorted(set(files))
    
    def run(self, root_dir: str):
        """Run the fixer on all files in the directory."""
        root_path = Path(root_dir)
        print(f"Phase 2: Fixing logging parameter order issues in {root_path}...")
        print("="*60)
        
        files = self.find_files_with_issues(root_path)
        print(f"Found {len(files)} files with potential logging parameter issues\n")
        
        for i, filepath in enumerate(files, 1):
            rel_path = filepath.relative_to(root_path)
            print(f"[{i}/{len(files)}] Processing {rel_path}...")
            self.process_file(filepath)
            self.files_processed += 1
        
        print(f"\n{'='*60}")
        print(f"Phase 2 Summary:")
        print(f"  Files processed: {self.files_processed}")
        print(f"  Files modified: {self.files_modified}")
        print(f"  Total fixes: {self.fixed_count}")
        print(f"\nPatterns fixed:")
        for method, count in sorted(self.patterns_found.items()):
            print(f"  {method}: {count}")
        print(f"{'='*60}")

if __name__ == "__main__":
    if len(sys.argv) > 1:
        root_dir = sys.argv[1]
    else:
        root_dir = "DayTradinPlatform"
    
    fixer = LoggingParameterFixer()
    fixer.run(root_dir)