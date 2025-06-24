#!/usr/bin/env python3
"""
Fix common nullable reference warnings in C# code
Focuses on CS8618 (non-nullable property/field uninitialized)
"""

import os
import re
import sys
from pathlib import Path
from typing import List, Tuple

def fix_property_initialization(content: str) -> Tuple[str, int]:
    """Fix uninitialized non-nullable properties by adding = string.Empty or appropriate defaults"""
    fixes = 0
    lines = content.split('\n')
    modified_lines = []
    
    for i, line in enumerate(lines):
        modified_line = line
        
        # Pattern 1: public string PropertyName { get; set; }
        # Fix: public string PropertyName { get; set; } = string.Empty;
        string_prop_pattern = r'(\s*public\s+string\s+\w+\s*{\s*get;\s*(?:set;|init;)\s*})(?!\s*=)'
        if re.search(string_prop_pattern, line) and 'string?' not in line:
            modified_line = re.sub(string_prop_pattern, r'\1 = string.Empty;', line)
            if modified_line != line:
                fixes += 1
                print(f"  Fixed string property: {line.strip()}")
        
        # Pattern 2: public List<T> PropertyName { get; set; }
        # Fix: public List<T> PropertyName { get; set; } = new();
        list_prop_pattern = r'(\s*public\s+(?:List|IList|HashSet|Dictionary|IEnumerable|ICollection)<[^>]+>\s+\w+\s*{\s*get;\s*(?:set;|init;)\s*})(?!\s*=)'
        if re.search(list_prop_pattern, line):
            modified_line = re.sub(list_prop_pattern, r'\1 = new();', line)
            if modified_line != line:
                fixes += 1
                print(f"  Fixed collection property: {line.strip()}")
        
        # Pattern 3: public T[] PropertyName { get; set; }
        # Fix: public T[] PropertyName { get; set; } = Array.Empty<T>();
        array_pattern = r'(\s*public\s+(\w+)\[\]\s+(\w+)\s*{\s*get;\s*(?:set;|init;)\s*})(?!\s*=)'
        match = re.search(array_pattern, line)
        if match:
            type_name = match.group(2)
            modified_line = re.sub(array_pattern, rf'\1 = Array.Empty<{type_name}>();', line)
            if modified_line != line:
                fixes += 1
                print(f"  Fixed array property: {line.strip()}")
        
        modified_lines.append(modified_line)
    
    return '\n'.join(modified_lines), fixes

def fix_constructor_initialization(content: str) -> Tuple[str, int]:
    """Add constructor initialization for non-nullable reference types"""
    fixes = 0
    
    # Find classes with uninitialized fields
    class_pattern = r'public\s+(?:partial\s+)?class\s+(\w+)(?:\s*:\s*[^{]+)?'
    classes = re.finditer(class_pattern, content)
    
    for class_match in classes:
        class_name = class_match.group(1)
        # Skip if it's a record or has primary constructor
        if 'record' in content[max(0, class_match.start()-50):class_match.start()]:
            continue
            
        # Find uninitialized private fields in this class
        class_start = class_match.end()
        next_class = re.search(r'public\s+(?:partial\s+)?class\s+\w+', content[class_start:])
        class_end = class_start + next_class.start() if next_class else len(content)
        
        class_content = content[class_start:class_end]
        
        # Check if class needs parameterless constructor
        needs_ctor = False
        if 'private readonly string' in class_content and '= string.Empty' not in class_content:
            needs_ctor = True
        
        if needs_ctor and f'public {class_name}()' not in class_content:
            # Add parameterless constructor
            ctor_insert_pos = class_content.find('{') + 1
            if ctor_insert_pos > 0:
                indent = '        '
                ctor = f'\n{indent}public {class_name}()\n{indent}{{\n{indent}    // Initialize non-nullable fields\n{indent}}}\n'
                new_content = class_content[:ctor_insert_pos] + ctor + class_content[ctor_insert_pos:]
                content = content[:class_start] + new_content + content[class_end:]
                fixes += 1
                print(f"  Added constructor to {class_name}")
    
    return content, fixes

def process_file(file_path: Path) -> int:
    """Process a single C# file"""
    try:
        content = file_path.read_text(encoding='utf-8')
        original_content = content
        total_fixes = 0
        
        # Apply fixes
        content, fixes = fix_property_initialization(content)
        total_fixes += fixes
        
        # Only apply constructor fixes to non-model files
        if 'Models' not in str(file_path) and 'record' not in content:
            content, fixes = fix_constructor_initialization(content)
            total_fixes += fixes
        
        # Write back if changed
        if content != original_content:
            file_path.write_text(content, encoding='utf-8')
            print(f"Fixed {file_path.name}: {total_fixes} fixes")
            return total_fixes
        
        return 0
    except Exception as e:
        print(f"Error processing {file_path}: {e}")
        return 0

def main():
    """Main entry point"""
    base_path = Path('/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform')
    
    # Target the most problematic projects first
    target_projects = [
        'TradingPlatform.Core',
        'TradingPlatform.Foundation',
        'TradingPlatform.Common',
        'TradingPlatform.DataIngestion',
        'TradingPlatform.Screening'
    ]
    
    total_fixes = 0
    
    for project in target_projects:
        project_path = base_path / project
        if not project_path.exists():
            print(f"Project {project} not found")
            continue
            
        print(f"\nProcessing {project}...")
        
        # Find all C# files
        cs_files = list(project_path.rglob('*.cs'))
        
        for cs_file in cs_files:
            # Skip generated files
            if 'obj' in cs_file.parts or 'bin' in cs_file.parts:
                continue
            
            fixes = process_file(cs_file)
            total_fixes += fixes
    
    print(f"\nTotal fixes applied: {total_fixes}")
    return 0

if __name__ == "__main__":
    sys.exit(main())