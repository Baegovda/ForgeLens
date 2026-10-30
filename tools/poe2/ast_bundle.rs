use poe_data_tools::file_parsers::{FileParser, ast::ASTParser};
fn main() {
    let a: Vec<String> = std::env::args().collect();
    let bytes = std::fs::read(&a[1]).unwrap();
    let ast = ASTParser.parse(&bytes).unwrap();
    let b = ast.bundle.expect("no bundle");
    let data = b.read_all().unwrap();
    std::fs::write(&a[2], &data).unwrap();
    eprintln!("ok {} bytes", data.len());
}
