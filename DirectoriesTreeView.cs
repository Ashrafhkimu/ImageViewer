using System;
using System.IO;
using System.Windows.Forms;

namespace ImageViewer
{
	/// <summary>
	/// TreeView для отображения структуры папок на файловой системе.
	/// Подпапки подгружаются динамически при разворачивании узлов дерева.
	/// </summary>
	class DirectoriesTreeView : TreeView
	{
		/// <summary>
		/// Обработчик выбора папки в дереве
		/// </summary>
		public event DirectoryTreeViewEventHandler AfterDirectorySelect;

		/// <summary>
		/// Конструктор
		/// </summary>
		public DirectoriesTreeView()
		{
		}

		/// <summary>
		/// Добавить в дерево корневые узлы, соответствующие дискам на файлой системе
		/// </summary>
		public void Load()
		{
			foreach( DriveInfo d in DriveInfo.GetDrives() )
				Nodes.Add( CreateNode( d.Name, d.Name ) );
		}

		/// <summary>
		/// Динамически подгружаем подпапки при разворачивании узла дерева
		/// </summary>
		/// <param name="e"></param>
		protected override void OnBeforeExpand( TreeViewCancelEventArgs e )
		{
			NodeInfo nodeInfo = ( NodeInfo )e.Node.Tag;
			if( ! nodeInfo.Loaded )
				PopulateNode( e.Node );

			base.OnBeforeExpand( e );
		}

		/// <summary>
		/// При возникновении события AfterSelect вызываем наше собственное AfterDirectorySelect,
		/// обработчику которого передаём полный путь к выбранной в дереве папке
		/// </summary>
		/// <param name="e"></param>
		protected override void OnAfterSelect( TreeViewEventArgs e )
		{
			if( AfterDirectorySelect != null )
			{
				string dirpath = ( ( NodeInfo )e.Node.Tag ).DirectoryPath;
				AfterDirectorySelect( this, new DirectoryTreeViewEventArgs( e, dirpath ) );
			}

			base.OnAfterSelect( e );
		}

		/// <summary>
		/// Заполнить дочерние узлы заданного узла дерева (список подпапок)
		/// </summary>
		/// <param name="node"></param>
		private void PopulateNode( TreeNode node )
		{
			NodeInfo nodeInfo = ( NodeInfo )node.Tag;

			node.Nodes.Clear();

			try
			{
				DirectoryInfo dir = new DirectoryInfo( nodeInfo.DirectoryPath );
				foreach( DirectoryInfo subdir in dir.GetDirectories() )
					node.Nodes.Add( CreateNode( subdir.Name, subdir.FullName ) );
			}
			catch( Exception e )
			{
				MessageBox.Show( this, e.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning );
			}
			finally
			{
				nodeInfo.Loaded = true;
			}
		}

		/// <summary>
		/// Создать узел дерева, соответствующий папке
		/// </summary>
		/// <param name="text">Текст узла</param>
		/// <param name="dirpath">Полный путь к папке, соответствующей узлу</param>
		/// <returns>Созданный узел дерева</returns>
		private static TreeNode CreateNode( string text, string dirpath )
		{
			TreeNode node = new TreeNode( text );
			node.Tag = new NodeInfo( dirpath );
			// добавляем пустой дочерний элемент, чтобы сделать узел разворачиваемым
			node.Nodes.Add( new TreeNode() );
			return node;
		}

		/// <summary>
		/// Структура, описывающая узел дерева
		/// </summary>
		class NodeInfo
		{
			/// <summary>
			/// Полный путь к папке на файловой системе, которой соответствует узел
			/// </summary>
			public string DirectoryPath;

			/// <summary>
			/// Прогружены ли уже дочерние узлы (список подпапок)
			/// </summary>
			public bool Loaded;

			/// <summary>
			/// Конструктор
			/// </summary>
			/// <param name="dirpath">Полный путь к папке на файловой системе, которой соответствует узел</param>
			public NodeInfo( string dirpath ) 
			{
				DirectoryPath = dirpath;
			}
		}
	}

	/// <summary>
	/// Обработчик выбора папки в дереве DirectoriesTreeView
	/// </summary>
	/// <param name="sender">Экземпляр DirectoriesTreeView</param>
	/// <param name="args">Параметры</param>
	delegate void DirectoryTreeViewEventHandler( object sender, DirectoryTreeViewEventArgs args);

	/// <summary>
	/// Параметры события выбора папки в дереве DirectoriesTreeView
	/// </summary>
	class DirectoryTreeViewEventArgs : TreeViewEventArgs
	{	
		/// <summary>
		/// Полный путь к папке
		/// </summary>
		public string DirectoryPath { get; private set; }

		/// <summary>
		/// Конструктор
		/// </summary>
		/// <param name="e"></param>
		/// <param name="dirpath">Полный путь к папке</param>
		public DirectoryTreeViewEventArgs( TreeViewEventArgs e, string dirpath ) : base( e.Node, e.Action )
		{
			DirectoryPath = dirpath;
		}
	}
}
