import os
import sys
import re
import json
import pdfplumber

# --- CONFIGURATION ---
DEBUG_MODE = True 
LINE_LIMIT = 100  # Increased limit to see the first few full questions

def clean_text(text):
    return re.sub(r'\s+', ' ', text).strip()

def process_pdf_golden_source(pdf_path):
    print(f"\n{'='*60}")
    print(f"[*] PROCESSING GOLDEN SOURCE: {os.path.basename(pdf_path)}")
    print(f"{'='*60}")
    
    questions = []
    current_q = None
    state = "SEEKING" # States: SEEKING, QUESTION, OPTIONS, EXPLANATION
    
    # Precise Patterns based on your new discovery
    re_q_start = re.compile(r'^#(\d+)\.\s*(.*)')       # Matches #1. When the tester...
    re_option  = re.compile(r'^([a-d])\.\s+(.*)')      # Matches a. Gaining confidence
    re_answer  = re.compile(r'^([A-D])\s+is\s+correct', re.IGNORECASE)

    try:
        with pdfplumber.open(pdf_path) as pdf:
            line_count = 0
            for page in pdf.pages:
                text = page.extract_text()
                if not text: continue
                
                for line in text.split('\n'):
                    line = line.strip()
                    if not line: continue
                    line_count += 1

                    # 1. Check for New Question Start (#1.)
                    q_match = re_q_start.match(line)
                    if q_match:
                        if DEBUG_MODE: print(f"L{line_count}: [NEW Q] -> {q_match.group(1)}")
                        if current_q: questions.append(current_q)
                        current_q = {
                            "Type": "ExamQuestion",
                            "ID": q_match.group(1),
                            "Term": q_match.group(2),
                            "Variants": [],
                            "CorrectAnswer": "",
                            "Definition": "" # This will store the Explanation
                        }
                        state = "QUESTION"
                        continue

                    # 2. Check for Options (a. b. c. d.)
                    opt_match = re_option.match(line)
                    if opt_match and current_q:
                        if DEBUG_MODE and line_count < LINE_LIMIT: print(f"  > OPT: {opt_match.group(1)}")
                        current_q["Variants"].append({
                            "Label": opt_match.group(1).upper(),
                            "Text": opt_match.group(2)
                        })
                        state = "OPTIONS"
                        continue

                    # 3. Check for Answer Line (C is correct...)
                    ans_match = re_answer.match(line)
                    if ans_match and current_q:
                        if DEBUG_MODE: print(f"  > ANS FOUND: {ans_match.group(1)}")
                        current_q["CorrectAnswer"] = ans_match.group(1).upper()
                        
                        # Extract initial explanation text if it exists on the same line
                        parts = re.split(r'is correct', line, flags=re.IGNORECASE)
                        if len(parts) > 1:
                            current_q["Definition"] = parts[1].strip(" per the syllabus.").strip()
                        
                        state = "EXPLANATION"
                        continue

                    # 4. Handle Multiline Text based on current State
                    if current_q:
                        if state == "QUESTION":
                            current_q["Term"] += " " + line
                        elif state == "OPTIONS" and current_q["Variants"]:
                            current_q["Variants"][-1]["Text"] += " " + line
                        elif state == "EXPLANATION":
                            current_q["Definition"] += " " + line

        if current_q: questions.append(current_q)
        
    except Exception as e:
        print(f"[!] ERROR: {e}")

    return questions

def run_pipeline(input_file):
    ext = os.path.splitext(input_file)[1].lower()
    if ext != '.pdf':
        print(f"[*] Skipping {input_file}")
        return

    results = process_pdf_golden_source(input_file)
    
    if not results:
        print(f"[!] Result: 0 items extracted. Check if PDF text starts with #1.")
        return

    # Generate Output Filename
    base_name = os.path.splitext(os.path.basename(input_file))[0]
    output_file = os.path.join(os.path.dirname(input_file), f"{base_name}_seed.json")

    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump(results, f, indent=4, ensure_ascii=False)
    
    print(f"\n[+] Success! Created {output_file}")
    print(f"[+] Total Questions Harvested: {len(results)}")

if __name__ == "__main__":
    if len(sys.argv) > 1:
        run_pipeline(sys.argv[1])
    else:
        print("[!] Drop a PDF onto the script or batch file.")