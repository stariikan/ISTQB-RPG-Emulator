import os
import sys
import re
import json
import pdfplumber

def clean(text):
    return re.sub(r'\s+', ' ', text).strip()

def is_footer_or_header(line):
    """Filter out the PDF page headers and footers so they don't corrupt the text."""
    ignore_phrases = [
        "International Software Testing Qualifications Board",
        "Sample Exam – Questions",
        "Sample Exam – Answers",
        "Appendix: Additional Questions",
        "Certified Tester, Foundation Level"
    ]
    # Check if the line contains any of the ignore phrases or starts with "Version 1."
    if any(phrase in line for phrase in ignore_phrases) or line.startswith("Version 1."):
        return True
    return False

def extract_questions(q_path):
    print(f"[*] Harvesting Questions from: {os.path.basename(q_path)}")
    questions = {}
    
    # 1. STRICT MATCH: "Question #4 (1 Point)" or "Question #A1 (1 Point)"
    # We capture the alphanumeric ID (e.g., "4" or "A1")
    re_q = re.compile(r'^Question\s*#\s*([A-Za-z0-9]+)\s*\(\d+\s*Points?\)', re.IGNORECASE)
    
    # 2. MATCH VARIANTS: "a) Text...", "b) Text..."
    re_opt = re.compile(r'^([a-e])\)\s+(.*)', re.IGNORECASE)
    
    # 3. MATCH SELECTION RULES: "Select ONE option.", "Select TWO options."
    re_select = re.compile(r'^Select\s+(ONE|TWO|THREE|FOUR)\s+option', re.IGNORECASE)

    with pdfplumber.open(q_path) as pdf:
        curr_id = None
        for page in pdf.pages:
            text = page.extract_text()
            if not text:
                continue
                
            for line in text.split('\n'):
                line = line.strip()
                
                # Skip empty lines and PDF headers/footers
                if not line or is_footer_or_header(line):
                    continue

                # TRIGGER 1: Found a new Question
                q_match = re_q.search(line)
                if q_match:
                    curr_id = q_match.group(1).upper()
                    questions[curr_id] = {
                        "ID": curr_id, 
                        "Question": "", 
                        "SelectionType": "Select ONE option.", # Default
                        "Variants": [], 
                        "Answer": "", 
                        "Explanation": ""
                    }
                    
                    # If there is text on the exact same line after "(1 Point)", grab it
                    rest_of_line = line[q_match.end():].strip()
                    if rest_of_line:
                        questions[curr_id]["Question"] += rest_of_line + " "
                    continue

                # If we haven't found our first question yet, ignore random text
                if not curr_id:
                    continue

                # TRIGGER 2: Check for "Select ONE option."
                sel_match = re_select.search(line)
                if sel_match:
                    num_options = sel_match.group(1).upper()
                    plural = "option." if num_options == "ONE" else "options."
                    questions[curr_id]["SelectionType"] = f"Select {num_options} {plural}"
                    continue 

                # TRIGGER 3: Found a Variant (a, b, c, d)
                opt_match = re_opt.match(line)
                if opt_match:
                    questions[curr_id]["Variants"].append({
                        "Label": opt_match.group(1).upper(), 
                        "Text": opt_match.group(2).strip()
                    })
                    continue
                
                # TRIGGER 4: Continuation Text
                # If we haven't found any variants yet, this text belongs to the Question.
                if len(questions[curr_id]["Variants"]) == 0:
                    questions[curr_id]["Question"] += line + " "
                # If we already found variants, this text belongs to the bottom of the last variant.
                else:
                    questions[curr_id]["Variants"][-1]["Text"] += " " + line

    # Final text cleanup (removes weird PDF line-break spacing)
    for q in questions.values():
        q["Question"] = clean(q["Question"])
        for v in q["Variants"]:
            v["Text"] = clean(v["Text"])

    return questions

def extract_answers(a_path, questions):
    print(f"[*] Harvesting Answers from Table in: {os.path.basename(a_path)}")
    
    with pdfplumber.open(a_path) as pdf:
        for page in pdf.pages:
            tables = page.extract_tables()
            for table in tables:
                for row in table:
                    # Skip empty rows or rows missing a question number
                    if not row or not row[0]: 
                        continue
                    
                    cells = [str(cell).replace('\n', ' ').strip() if cell else "" for cell in row]
                    q_id = cells[0].upper()
                    
                    # Check if this ID exists in our parsed questions (handles 1-40 and A1-A26)
                    if q_id in questions:
                        # Index 1 is Correct Answer (e.g., "c", "a, b")
                        raw_answers = cells[1].upper()
                        correct_letters = re.findall(r'[A-E]', raw_answers)
                        questions[q_id]["Answer"] = ", ".join(correct_letters)
                        
                        # Index 2 is the Explanation block
                        full_explanation = cells[2]
                        extracted_explanations = []
                        
                        # Extract ONLY the text block for the correct letter(s)
                        for letter in correct_letters:
                            # Regex: Look for "C) ", capture everything until the next "[A-E]) " or end of string
                            pattern = rf"(?i)\b{letter}\)\s*(.*?)(?=\b[a-e]\)\s|$)"
                            match = re.search(pattern, full_explanation)
                            
                            if match:
                                extracted_explanations.append(f"{letter}) {clean(match.group(1))}")
                        
                        # Save the tailored explanation (or fallback to full if parsing fails)
                        if extracted_explanations:
                            questions[q_id]["Explanation"] = " ".join(extracted_explanations)
                        else:
                            questions[q_id]["Explanation"] = clean(full_explanation)

    return questions

def run_split_pipeline(q_file, a_file):
    q_data = extract_questions(q_file)
    final_data = extract_answers(a_file, q_data)
    
    output_file = f"{os.path.splitext(q_file)[0]}_COMPLETE.json"
    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump(list(final_data.values()), f, indent=4, ensure_ascii=False)
    
    print(f"\n[+] SUCCESS: Merged {len(final_data)} questions into {output_file}")

if __name__ == "__main__":
    if len(sys.argv) == 3:
        run_split_pipeline(sys.argv[1], sys.argv[2])
    else:
        print("Usage: python process_split_exam.py <Questions.pdf> <Answers.pdf>")