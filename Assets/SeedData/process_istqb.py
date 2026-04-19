import json

input_file = "Terms_1_seed.json"
output_file = "Terms_1_seed_cleaned.json"

with open(input_file, "r", encoding="utf-8") as f:
    data = json.load(f)

seen = set()
cleaned_data = []

for item in data:
    item_str = json.dumps(item, sort_keys=True)
    
    if item_str not in seen:
        seen.add(item_str)
        cleaned_data.append(item)

with open(output_file, "w", encoding="utf-8") as f:
    json.dump(cleaned_data, f, indent=2, ensure_ascii=False)

print("Exact duplicates removed")