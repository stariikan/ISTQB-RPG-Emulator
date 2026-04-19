import os
import sys
import re
import json

def process_to_json(input_file, output_file):
    print(f"[*] Reading from {input_file}...")
    
    # 1. Resilient Encoding for Windows/Excel exports
    encodings = ['utf-8-sig', 'utf-8', 'utf-16', 'windows-1252']
    content = None
    for enc in encodings:
        try:
            with open(input_file, 'r', encoding=enc) as f:
                content = f.read()
            break
        except UnicodeDecodeError:
            continue
            
    if not content:
        print("[!] Error: Could not decode file.")
        return

    # 2. Split into blocks using your '&&' delineator
    raw_cards = content.split('&&')
    
    db_ready_data = []

    for idx, card in enumerate(raw_cards):
        card = card.strip()
        if not card: continue
        
        # 3. Split by your "@" delineator (handling quotes if present)
        # Your export format: "Front"@"Back" or "Front"@"Back"@"Image"
        parts = card.split('"@"')
        
        if len(parts) >= 2:
            # Clean up residual quotes and whitespace
            front = parts[0].strip('" \n\t')
            back = parts[1].strip('" \n\t')
            image_url = parts[2].strip('" \n\t') if len(parts) == 3 else None
            
            # 4. Clean definition for UI (flatten internal newlines)
            back = back.replace('\n', ' ')
            back = re.sub(r'\s{2,}', ' ', back)
            
            # 5. Categorize for our C# Model
            # ISTQB terms are usually short; syllabus concepts are long/bulleted.
            is_concept = '•' in back or len(back) > 350
            item_type = "SyllabusConcept" if is_concept else "GlossaryTerm"

            db_ready_data.append({
                "Type": item_type,
                "Term": front,
                "Definition": back,
                "ImageUrl": image_url
            })
            
    # Write the standard JSON
    with open(output_file, 'w', encoding='utf-8') as out_f:
        json.dump(db_ready_data, out_f, indent=4, ensure_ascii=False)
            
    print(f"[+] Success! Created {output_file} with {len(db_ready_data)} items.")

if __name__ == "__main__":
    if len(sys.argv) > 1:
        input_file = sys.argv[1]
    else:
        input_file = 'csv-export.csv'

    input_dir = os.path.dirname(input_file)
    base_name = os.path.splitext(os.path.basename(input_file))[0]
    output_file = os.path.join(input_dir, f"{base_name}_seed.json")
    
    process_to_json(input_file, output_file)