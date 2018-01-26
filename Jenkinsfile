node {
    stage('Clone'){
		checkout scm
	}

	stage('Clean'){
		sh('git clean -xdff')
	}

    stage('Build'){
		msbuild()
	}

	stage('Publish'){
		publish("Jamcast5/bin/Release/Packager.exe")
	}
    
	stage('Archive'){
		archive '**/bin/**/'
	}

		stage('Post-Build'){
		step([$class: 'WarningsPublisher', canComputeNew: false, canResolveRelativePaths: false, consoleParsers: [[parserName: 'MSBuild']], defaultEncoding: '', excludePattern: '', healthy: '', includePattern: '', messagesPattern: '', unHealthy: ''])
	}

}