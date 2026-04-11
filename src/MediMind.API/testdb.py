import psycopg2, sys
try:
    psycopg2.connect("host=localhost dbname=postgres user=dani")
    print("Success: user dani")
    sys.exit(0)
except Exception as e:
    print(f"Failed dani: {e}")

try:
    psycopg2.connect("host=localhost dbname=postgres user=postgres password=postgres")
    print("Success: user postgres pwd postgres")
    sys.exit(0)
except Exception as e:
    print(f"Failed postgres: {e}")

try:
    psycopg2.connect("host=localhost dbname=postgres user=postgres")
    print("Success: user postgres no pwd")
    sys.exit(0)
except Exception as e:
    print(f"Failed postgres no pwd: {e}")
